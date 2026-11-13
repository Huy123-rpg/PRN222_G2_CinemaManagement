using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.ImpleService;

public sealed class ScreeningScheduleService(IScreeningRepository screenings) : IScreeningScheduleService
{
    private const int CleanupMinutes = 15;
    private static TimeZoneInfo VietnamTimeZone => TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public async Task<ScreeningScheduleViewModel> GetScheduleAsync(DateOnly date, int? auditoriumId, int dayCount)
    {
        var fromUtc = ToUtc(date.ToDateTime(TimeOnly.MinValue));
        var toUtc = ToUtc(date.AddDays(dayCount).ToDateTime(TimeOnly.MinValue));
        var rows = await screenings.GetScreeningsAsync(fromUtc, toUtc, auditoriumId, default);
        var rooms = await screenings.GetActiveAuditoriumsAsync(default);

        var items = new List<ScreeningItemViewModel>();
        foreach (var s in rows)
        {
            var canEdit = s.Status == "Scheduled" && s.StartAt > DateTime.UtcNow
                && !await screenings.HasBookingOrTicketAsync(s.ScreeningId, default);
            items.Add(new ScreeningItemViewModel
            {
                ScreeningId = s.ScreeningId,
                AuditoriumId = s.AuditoriumId,
                MovieTitle = s.Movie.Title,
                AuditoriumName = s.Auditorium.Name,
                StartAtLocal = FromUtc(s.StartAt),
                EndAtLocal = FromUtc(s.EndAt),
                BasePrice = s.BasePrice,
                Status = s.Status,
                CanEdit = canEdit
            });
        }

        return new ScreeningScheduleViewModel
        {
            Date = date,
            SelectedDate = date,
            DayCount = dayCount,
            AuditoriumId = auditoriumId,
            Auditoriums = rooms.Select(ToRoomOption).ToList(),
            Screenings = items
        };
    }

    public async Task<ScreeningFormViewModel> GetCreateFormAsync() =>
        await PopulateOptionsAsync(new ScreeningFormViewModel());

    public async Task<ScreeningBatchViewModel> GetBatchFormAsync(int? movieId, CancellationToken cancellationToken = default)
    {
        var movies = await screenings.GetSchedulableMoviesAsync(cancellationToken);
        var rooms = await screenings.GetActiveAuditoriumsAsync(cancellationToken);
        var selectedMovie = movieId.HasValue
            ? movies.FirstOrDefault(m => m.MovieId == movieId.Value)
            : movies.FirstOrDefault();
        if (movieId.HasValue && selectedMovie == null)
            throw new KeyNotFoundException("Không tìm thấy phim có thể lập lịch.");

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));
        var startDate = selectedMovie?.ReleaseDate is { } releaseDate && releaseDate > today ? releaseDate : today;
        return new ScreeningBatchViewModel
        {
            MovieId = selectedMovie?.MovieId ?? 0,
            MovieTitle = selectedMovie?.Title ?? "",
            DurationMinutes = selectedMovie?.DurationMinutes ?? 0,
            ReleaseDate = selectedMovie?.ReleaseDate,
            StartDate = startDate,
            EndDate = startDate.AddDays(6),
            Movies = movies.Select(m => new MovieOptionViewModel { Id = m.MovieId, Title = m.Title, DurationMinutes = m.DurationMinutes, ReleaseDate = m.ReleaseDate }).ToList(),
            Auditoriums = rooms.Select(ToRoomOption).ToList()
        };
    }

    public async Task<List<ScreeningBatchConflictViewModel>> CheckBatchConflictsAsync(
        ScreeningBatchViewModel model,
        CancellationToken cancellationToken = default)
    {
        ValidateBatchFields(model);
        var movie = await GetSchedulableMovieAsync(model.MovieId, cancellationToken);
        ValidateBatchDates(model, movie);

        var activeRooms = await screenings.GetActiveAuditoriumsAsync(cancellationToken);
        var roomIds = activeRooms.Select(r => r.AuditoriumId).ToHashSet();
        if (model.Slots.Any(s => !roomIds.Contains(s.AuditoriumId)))
            throw new InvalidOperationException("Một suất đang chọn phòng không hoạt động hoặc không tồn tại.");

        foreach (var roomId in model.Slots.Select(s => s.AuditoriumId).Distinct())
            await GetActiveSeatsAsync(roomId, cancellationToken);

        var candidates = model.Slots.Select((slot, index) =>
        {
            if (!slot.Date.HasValue || !slot.StartTime.HasValue)
                throw new InvalidOperationException($"Suất số {index + 1} cần có ngày và giờ bắt đầu.");
            if (slot.Date.Value < model.StartDate!.Value || slot.Date.Value > model.EndDate!.Value)
                throw new InvalidOperationException($"Suất số {index + 1} nằm ngoài khoảng ngày đã chọn.");
            if (movie.ReleaseDate.HasValue && slot.Date.Value < movie.ReleaseDate.Value)
                throw new InvalidOperationException($"Suất số {index + 1} trước ngày khởi chiếu {movie.ReleaseDate:dd/MM/yyyy}.");

            var startLocal = slot.Date.Value.ToDateTime(slot.StartTime.Value);
            ValidateFutureStart(startLocal);
            var startUtc = ToUtc(startLocal);
            return new BatchCandidate(index, slot.AuditoriumId, startUtc, startUtc.AddMinutes(movie.DurationMinutes));
        }).OrderBy(c => c.StartUtc).ToList();

        var fromUtc = candidates.Min(c => c.StartUtc);
        var toUtc = candidates.Max(c => c.EndUtc.AddMinutes(CleanupMinutes));
        var existing = await screenings.GetScreeningsOverlappingAsync(
            candidates.Select(c => c.AuditoriumId).Distinct().ToArray(), fromUtc, toUtc, cancellationToken);
        var conflicts = new List<ScreeningBatchConflictViewModel>();

        foreach (var candidate in candidates)
        foreach (var other in existing.Where(s => s.AuditoriumId == candidate.AuditoriumId
            && candidate.StartUtc < s.EndAt.AddMinutes(s.CleanupMinutes)
            && s.StartAt < candidate.EndUtc.AddMinutes(CleanupMinutes)))
        {
            conflicts.Add(new ScreeningBatchConflictViewModel
            {
                SlotIndex = candidate.Index,
                Message = "Trùng lịch hoặc chưa đủ 15 phút dọn phòng.",
                ExistingMovieTitle = other.Movie.Title,
                AuditoriumName = other.Auditorium.Name,
                ExistingStartAtLocal = FromUtc(other.StartAt),
                ExistingEndAtLocal = FromUtc(other.EndAt)
            });
        }

        for (var i = 0; i < candidates.Count; i++)
        for (var j = i + 1; j < candidates.Count; j++)
        {
            var first = candidates[i];
            var second = candidates[j];
            if (first.AuditoriumId != second.AuditoriumId
                || first.StartUtc >= second.EndUtc.AddMinutes(CleanupMinutes)
                || second.StartUtc >= first.EndUtc.AddMinutes(CleanupMinutes))
                continue;

            conflicts.Add(new ScreeningBatchConflictViewModel
            {
                SlotIndex = first.Index,
                Message = $"Bị trùng với suất số {second.Index + 1} trong đợt đang tạo.",
                AuditoriumName = activeRooms.First(r => r.AuditoriumId == first.AuditoriumId).Name,
                ExistingStartAtLocal = FromUtc(second.StartUtc),
                ExistingEndAtLocal = FromUtc(second.EndUtc)
            });
            conflicts.Add(new ScreeningBatchConflictViewModel
            {
                SlotIndex = second.Index,
                Message = $"Bị trùng với suất số {first.Index + 1} trong đợt đang tạo.",
                AuditoriumName = activeRooms.First(r => r.AuditoriumId == second.AuditoriumId).Name,
                ExistingStartAtLocal = FromUtc(first.StartUtc),
                ExistingEndAtLocal = FromUtc(first.EndUtc)
            });
        }

        return conflicts;
    }

    public Task CreateBatchAsync(ScreeningBatchViewModel model, CancellationToken cancellationToken = default)
    {
        ValidateBatchFields(model);
        ValidatePrice(model.BasePrice);
        return screenings.ExecuteSerializableAsync(async ct =>
        {
            var movie = await GetSchedulableMovieAsync(model.MovieId, ct);
            ValidateBatchDates(model, movie);
            var conflicts = await CheckBatchConflictsAsync(model, ct);
            if (conflicts.Count > 0)
                throw new ScreeningBatchException("Có suất bị xung đột. Hãy chỉnh lịch trước khi lưu.", conflicts);

            var rooms = model.Slots.Select(s => s.AuditoriumId).Distinct().ToArray();
            var seatsByRoom = new Dictionary<int, List<Seat>>();
            foreach (var roomId in rooms)
                seatsByRoom.Add(roomId, await GetActiveSeatsAsync(roomId, ct));

            var created = model.Slots.Select(slot =>
            {
                var startAt = ToUtc(slot.Date!.Value.ToDateTime(slot.StartTime!.Value));
                var screening = new Screening
                {
                    MovieId = movie.MovieId,
                    AuditoriumId = slot.AuditoriumId,
                    StartAt = startAt,
                    EndAt = startAt.AddMinutes(movie.DurationMinutes),
                    CleanupMinutes = CleanupMinutes,
                    BasePrice = model.BasePrice
                };
                screenings.Add(screening);
                return screening;
            }).ToList();

            await screenings.SaveChangesAsync(ct);
            foreach (var screening in created)
                screenings.AddScreeningSeats(BuildScreeningSeats(screening, seatsByRoom[screening.AuditoriumId]));
            await screenings.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    public async Task<ScreeningFormViewModel?> GetEditFormAsync(int screeningId)
    {
        var screening = await screenings.GetScreeningAsync(screeningId, default);
        if (screening == null) return null;

        var canSave = screening.Status == "Scheduled"
            && screening.StartAt > DateTime.UtcNow
            && !await screenings.HasBookingOrTicketAsync(screeningId, default);
        var model = new ScreeningFormViewModel
        {
            ScreeningId = screeningId,
            MovieId = screening.MovieId,
            AuditoriumId = screening.AuditoriumId,
            StartAtLocal = FromUtc(screening.StartAt),
            BasePrice = screening.BasePrice,
            CanSave = canSave,
            ReadOnlyMessage = canSave ? null : "Chỉ có thể đổi lịch suất Scheduled trong tương lai khi chưa có booking hoặc vé."
        };
        return await PopulateOptionsAsync(model);
    }

    public async Task<List<ScreeningConflictViewModel>> CheckConflictsAsync(ScreeningFormViewModel model)
    {
        var movie = await GetSchedulableMovieAsync(model.MovieId, default);
        await EnsureAuditoriumActiveAsync(model.AuditoriumId, default);
        ValidateFutureStart(model.StartAtLocal);
        ValidateMovieReleaseDate(movie, model.StartAtLocal!.Value);
        var startUtc = ToUtc(model.StartAtLocal!.Value);
        var endUtc = startUtc.AddMinutes(movie.DurationMinutes);
        return (await screenings.GetScheduleConflictsAsync(
                model.AuditoriumId, startUtc, endUtc, model.ScreeningId, default))
            .Select(ToConflict)
            .ToList();
    }

    public Task<Screening> CreateAsync(SaveScreeningRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePrice(request.BasePrice);
        ValidateFutureStart(request.StartAtLocal);
        return screenings.ExecuteSerializableAsync(async ct =>
        {
            var movie = await GetSchedulableMovieAsync(request.MovieId, ct);
            ValidateMovieReleaseDate(movie, request.StartAtLocal);
            await EnsureAuditoriumActiveAsync(request.AuditoriumId, ct);
            var seats = await GetActiveSeatsAsync(request.AuditoriumId, ct);
            var startAt = ToUtc(request.StartAtLocal);
            var endAt = startAt.AddMinutes(movie.DurationMinutes);
            await EnsureNoConflictAsync(request.AuditoriumId, startAt, endAt, null, ct);

            var screening = new Screening
            {
                MovieId = movie.MovieId,
                AuditoriumId = request.AuditoriumId,
                StartAt = startAt,
                EndAt = endAt,
                CleanupMinutes = CleanupMinutes,
                BasePrice = request.BasePrice
            };
            screenings.Add(screening);
            await screenings.SaveChangesAsync(ct);
            screenings.AddScreeningSeats(BuildScreeningSeats(screening, seats));
            await screenings.SaveChangesAsync(ct);
            return screening;
        }, cancellationToken);
    }

    public Task<Screening> UpdateAsync(int screeningId, SaveScreeningRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePrice(request.BasePrice);
        ValidateFutureStart(request.StartAtLocal);
        return screenings.ExecuteSerializableAsync(async ct =>
        {
            var screening = await screenings.GetScreeningAsync(screeningId, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy suất chiếu.");
            if (screening.Status != "Scheduled" || screening.StartAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Chỉ có thể đổi lịch suất Scheduled trong tương lai.");
            if (await screenings.HasBookingOrTicketAsync(screeningId, ct))
                throw new InvalidOperationException("Không thể đổi lịch suất đã có booking hoặc vé.");

            var movie = await GetSchedulableMovieAsync(request.MovieId, ct);
            ValidateMovieReleaseDate(movie, request.StartAtLocal);
            await EnsureAuditoriumActiveAsync(request.AuditoriumId, ct);
            var seats = await GetActiveSeatsAsync(request.AuditoriumId, ct);
            var startAt = ToUtc(request.StartAtLocal);
            var endAt = startAt.AddMinutes(movie.DurationMinutes);
            await EnsureNoConflictAsync(request.AuditoriumId, startAt, endAt, screeningId, ct);

            screening.MovieId = movie.MovieId;
            screening.AuditoriumId = request.AuditoriumId;
            screening.StartAt = startAt;
            screening.EndAt = endAt;
            screening.CleanupMinutes = CleanupMinutes;
            screening.BasePrice = request.BasePrice;
            screenings.RemoveScreeningSeats(screening.ScreeningSeats);
            screenings.AddScreeningSeats(BuildScreeningSeats(screening, seats));
            await screenings.SaveChangesAsync(ct);
            return screening;
        }, cancellationToken);
    }

    private async Task<ScreeningFormViewModel> PopulateOptionsAsync(ScreeningFormViewModel model)
    {
        var movies = await screenings.GetSchedulableMoviesAsync(default);
        var rooms = await screenings.GetActiveAuditoriumsAsync(default);
        model.Movies = movies.Select(m => new MovieOptionViewModel
        {
            Id = m.MovieId,
            Title = m.Title,
            DurationMinutes = m.DurationMinutes,
            ReleaseDate = m.ReleaseDate
        }).ToList();
        model.Auditoriums = rooms.Select(ToRoomOption).ToList();
        return model;
    }

    private async Task<Movie> GetSchedulableMovieAsync(int movieId, CancellationToken ct)
    {
        var movie = await screenings.GetMovieAsync(movieId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy phim.");
        if (movie.Status == "Ended")
            throw new InvalidOperationException("Không thể lập lịch cho phim đã kết thúc.");
        if (movie.DurationMinutes <= 0)
            throw new InvalidOperationException("Thời lượng phim phải lớn hơn 0.");
        return movie;
    }

    private async Task EnsureAuditoriumActiveAsync(int auditoriumId, CancellationToken ct)
    {
        if (!await screenings.AuditoriumIsActiveAsync(auditoriumId, ct))
            throw new InvalidOperationException("Phòng chiếu không tồn tại hoặc đang không hoạt động.");
    }

    private async Task<List<Seat>> GetActiveSeatsAsync(int auditoriumId, CancellationToken ct)
    {
        var seats = await screenings.GetActiveSeatsAsync(auditoriumId, ct);
        if (seats.Count == 0)
            throw new InvalidOperationException("Phòng chiếu chưa có ghế hoạt động để tạo suất chiếu.");
        return seats;
    }

    private async Task EnsureNoConflictAsync(int auditoriumId, DateTime startAt, DateTime endAt, int? exceptId, CancellationToken ct)
    {
        var conflicts = await screenings.GetScheduleConflictsAsync(auditoriumId, startAt, endAt, exceptId, ct);
        if (conflicts.Count == 0) return;

        var exception = new ScreeningScheduleException("Phòng chiếu bị trùng lịch hoặc chưa đủ 15 phút dọn phòng.");
        exception.Conflicts.AddRange(conflicts.Select(ToConflict));
        throw exception;
    }

    private static List<ScreeningSeat> BuildScreeningSeats(Screening screening, IEnumerable<Seat> seats) =>
        seats.Select(seat => new ScreeningSeat
        {
            ScreeningId = screening.ScreeningId,
            AuditoriumId = screening.AuditoriumId,
            SeatId = seat.SeatId,
            Price = decimal.Round(screening.BasePrice * seat.SeatType.PriceMultiplier, 2, MidpointRounding.AwayFromZero),
            Status = "Available"
        }).ToList();

    private static ScreeningConflictViewModel ToConflict(Screening s) => new()
    {
        MovieTitle = s.Movie.Title,
        AuditoriumName = s.Auditorium.Name,
        StartAtLocal = FromUtc(s.StartAt),
        EndAtLocal = FromUtc(s.EndAt)
    };

    private static AuditoriumOptionViewModel ToRoomOption(Auditorium a) => new() { Id = a.AuditoriumId, Name = a.Name };

    private static DateTime ToUtc(DateTime localTime) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), VietnamTimeZone);

    private static DateTime FromUtc(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), VietnamTimeZone);

    private static void ValidateFutureStart(DateTime? startAtLocal)
    {
        if (!startAtLocal.HasValue || ToUtc(startAtLocal.Value) <= DateTime.UtcNow)
            throw new InvalidOperationException("Giờ bắt đầu suất chiếu phải nằm trong tương lai.");
    }

    private static void ValidateMovieReleaseDate(Movie movie, DateTime startAtLocal)
    {
        if (movie.ReleaseDate.HasValue && DateOnly.FromDateTime(startAtLocal) < movie.ReleaseDate.Value)
            throw new InvalidOperationException($"Không thể lập suất chiếu trước ngày khởi chiếu {movie.ReleaseDate:dd/MM/yyyy}.");
    }

    private static void ValidateBatchFields(ScreeningBatchViewModel model)
    {
        if (model.MovieId <= 0) throw new InvalidOperationException("Chọn phim cần lập lịch.");
        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
            throw new InvalidOperationException("Chọn ngày bắt đầu và ngày kết thúc.");
        if (model.EndDate < model.StartDate)
            throw new InvalidOperationException("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
        if (model.EndDate.Value.DayNumber - model.StartDate.Value.DayNumber > 29)
            throw new InvalidOperationException("Mỗi đợt lập lịch tối đa 30 ngày.");
        if (model.Slots.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một suất chiếu vào đợt.");
    }

    private static void ValidateBatchDates(ScreeningBatchViewModel model, Movie movie)
    {
        if (movie.ReleaseDate.HasValue && model.StartDate!.Value < movie.ReleaseDate.Value)
            throw new InvalidOperationException($"Ngày bắt đầu lập lịch không được trước ngày khởi chiếu {movie.ReleaseDate:dd/MM/yyyy}.");
        if (movie.ReleaseDate.HasValue && model.EndDate!.Value < movie.ReleaseDate.Value)
            throw new InvalidOperationException($"Ngày kết thúc lập lịch không được trước ngày khởi chiếu {movie.ReleaseDate:dd/MM/yyyy}.");
    }

    private sealed record BatchCandidate(int Index, int AuditoriumId, DateTime StartUtc, DateTime EndUtc);

    private static void ValidatePrice(decimal basePrice)
    {
        if (basePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(basePrice), "Giá suất chiếu không được âm.");
    }
}
