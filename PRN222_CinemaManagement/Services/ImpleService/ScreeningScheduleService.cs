using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

public sealed class ScreeningScheduleService(IScreeningRepository screenings) : IScreeningScheduleService
{
    private const int CleanupMinutes = 15;

    public Task<Screening> CreateAsync(SaveScreeningRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePrice(request.BasePrice);
        return screenings.ExecuteSerializableAsync(async ct =>
        {
            var movie = await GetMovieAsync(request.MovieId, ct);
            await EnsureAuditoriumActiveAsync(request.AuditoriumId, ct);
            var endAt = request.StartAt.AddMinutes(movie.DurationMinutes);
            await EnsureNoConflictAsync(request.AuditoriumId, request.StartAt, endAt, null, ct);

            var screening = new Screening
            {
                MovieId = movie.MovieId,
                AuditoriumId = request.AuditoriumId,
                StartAt = request.StartAt,
                EndAt = endAt,
                CleanupMinutes = CleanupMinutes,
                BasePrice = request.BasePrice
            };

            screenings.Add(screening);
            await screenings.SaveChangesAsync(ct);
            return screening;
        }, cancellationToken);
    }

    public Task<Screening> UpdateAsync(int screeningId, SaveScreeningRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePrice(request.BasePrice);
        return screenings.ExecuteSerializableAsync(async ct =>
        {
            var screening = await screenings.GetScreeningAsync(screeningId, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy suất chiếu.");
            if (await screenings.HasBookingOrTicketAsync(screeningId, ct))
                throw new InvalidOperationException("Không thể sửa suất chiếu đã có booking hoặc vé.");

            var movie = await GetMovieAsync(request.MovieId, ct);
            await EnsureAuditoriumActiveAsync(request.AuditoriumId, ct);
            var endAt = request.StartAt.AddMinutes(movie.DurationMinutes);
            await EnsureNoConflictAsync(request.AuditoriumId, request.StartAt, endAt, screeningId, ct);

            screening.MovieId = movie.MovieId;
            screening.AuditoriumId = request.AuditoriumId;
            screening.StartAt = request.StartAt;
            screening.EndAt = endAt;
            screening.CleanupMinutes = CleanupMinutes;
            screening.BasePrice = request.BasePrice;
            await screenings.SaveChangesAsync(ct);
            return screening;
        }, cancellationToken);
    }

    private async Task<Movie> GetMovieAsync(int movieId, CancellationToken cancellationToken)
    {
        var movie = await screenings.GetMovieAsync(movieId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phim.");
        if (movie.DurationMinutes <= 0)
            throw new InvalidOperationException("Thời lượng phim phải lớn hơn 0.");
        return movie;
    }

    private async Task EnsureAuditoriumActiveAsync(int auditoriumId, CancellationToken cancellationToken)
    {
        if (!await screenings.AuditoriumIsActiveAsync(auditoriumId, cancellationToken))
            throw new InvalidOperationException("Phòng chiếu không tồn tại hoặc đang không hoạt động.");
    }

    private async Task EnsureNoConflictAsync(int auditoriumId, DateTime startAt, DateTime endAt, int? exceptId, CancellationToken cancellationToken)
    {
        if (await screenings.HasScheduleConflictAsync(auditoriumId, startAt, endAt, exceptId, cancellationToken))
            throw new InvalidOperationException("Lịch bị trùng hoặc chưa đủ 15 phút dọn phòng trong auditorium.");
    }

    private static void ValidatePrice(decimal basePrice)
    {
        if (basePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(basePrice), "Giá suất chiếu không được âm.");
    }
}
