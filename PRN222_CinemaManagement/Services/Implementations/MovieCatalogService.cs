using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Models.DTOs;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Repositories.Interfaces;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Services.Implementations;

/// <summary>Xem phim, tìm kiếm/lọc phim, suất chiếu và sơ đồ ghế. Các phương thức chỉ đọc dữ liệu.</summary>
public class MovieCatalogService(
    IMovieRepository movieRepository,
    IShowtimeRepository showtimeRepository,
    IShowtimeSeatRepository showtimeSeatRepository,
    IBookingRepository bookingRepository,
    IBookingSupportService support) : IMovieCatalogService
{
    private static readonly HashSet<string> AllowedSortOptions = new(StringComparer.Ordinal)
    {
        BookingConstants.MovieSortOptions.TitleAsc,
        BookingConstants.MovieSortOptions.TitleDesc,
        BookingConstants.MovieSortOptions.Newest,
        BookingConstants.MovieSortOptions.DurationAsc,
        BookingConstants.MovieSortOptions.DurationDesc,
        BookingConstants.MovieSortOptions.ShowtimesDesc
    };

    public async Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default)
    {
        var movies = await movieRepository.GetShowingMoviesAsync(ct);
        return movies.Select(m => CreateMovieViewModel(m, 0)).ToList();
    }

    public async Task<MovieCatalogViewModel> GetMovieCatalogAsync(MovieFilterViewModel? filter, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var normalizedFilter = NormalizeFilter(filter);

        var criteria = new MovieSearchCriteria
        {
            Search = normalizedFilter.Search,
            GenreId = normalizedFilter.GenreId,
            AgeRating = normalizedFilter.AgeRating,
            ShowDate = normalizedFilter.ShowDate,
            SortBy = normalizedFilter.SortBy,
            OnlyWithShowtimes = normalizedFilter.OnlyWithShowtimes
        };

        // Tuần tự trên cùng một scoped DbContext.
        var options = await movieRepository.GetShowingFilterOptionsAsync(now, ct);
        var items = await movieRepository.SearchShowingMoviesAsync(criteria, now, ct);

        var todayVn = DateOnly.FromDateTime(BookingDisplay.VietnamTime(now));

        return new MovieCatalogViewModel
        {
            Filter = normalizedFilter,
            TotalShowingCount = options.TotalShowingCount,
            Movies = items.Select(MapMovieCard).ToList(),
            Genres = options.Genres.Select(g => new GenreOptionViewModel
            {
                GenreId = g.GenreId,
                GenreName = g.GenreName,
                MovieCount = g.MovieCount
            }).ToList(),
            AgeRatings = options.AgeRatings,
            ShowDates = options.ShowDates.Select(d => new ShowDateOptionViewModel
            {
                Date = d,
                Label = FormatShowDateLabel(d, todayVn)
            }).ToList()
        };
    }

    public async Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default)
    {
        var movie = await movieRepository.GetShowingMovieByIdAsync(movieId, ct);
        if (movie == null) return null;

        var showtimes = await showtimeRepository.GetUpcomingByMovieIdAsync(movieId, DateTime.UtcNow, ct);
        var model = new MovieDetailsViewModel { Movie = CreateMovieViewModel(movie, showtimes.Count) };

        foreach (var showtime in showtimes)
        {
            model.Showtimes.Add(new ShowtimeOptionViewModel
            {
                ShowtimeId = showtime.ShowtimeId,
                Auditorium = showtime.Auditorium.Name,
                StartAt = support.AsUtc(showtime.StartAt),
                BasePrice = showtime.BasePrice
            });
        }

        return model;
    }

    public async Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default)
    {
        var showtime = await support.GetOpenShowtimeAsync(showtimeId, ct);
        if (showtime == null) return null;

        var now = DateTime.UtcNow;
        Booking? myBooking = null;
        if (userId.HasValue)
        {
            // Chỉ trả mã đơn của người xem, không gửi người giữ ghế khác ra trình duyệt.
            myBooking = await bookingRepository.GetActivePendingOnlineBookingAsync(showtimeId, userId.Value, now, ct);
        }

        var showtimeSeats = await showtimeSeatRepository.GetByShowtimeIdWithDetailsAsync(showtimeId, ct);

        var model = new SeatMapViewModel
        {
            ShowtimeId = showtimeId,
            MovieTitle = showtime.Movie.Title,
            Auditorium = showtime.Auditorium.Name,
            StartAt = support.AsUtc(showtime.StartAt),
            ServerNow = now
        };

        if (myBooking != null)
        {
            model.BookingId = myBooking.BookingId;
            model.ExpiresAt = support.AsUtc(myBooking.ExpiresAt);
        }

        foreach (var seat in showtimeSeats)
        {
            var status = seat.Status;
            if (status != BookingConstants.ShowtimeSeatStatuses.Sold)
            {
                if (seat.Seat.Status != BookingConstants.PhysicalSeatStatuses.Active || !seat.Seat.SeatType.IsActive)
                {
                    status = BookingConstants.ShowtimeSeatStatuses.Blocked;
                }
                else if (status == BookingConstants.ShowtimeSeatStatuses.Held && seat.HoldExpiresAt <= now)
                {
                    // Hết hạn thì có thể chọn lại ngay, không cần chờ worker.
                    status = BookingConstants.ShowtimeSeatStatuses.Available;
                }
            }

            model.Seats.Add(new SeatViewModel
            {
                ShowtimeSeatId = seat.ShowtimeSeatId,
                RowLabel = seat.Seat.RowLabel,
                SeatNumber = seat.Seat.SeatNumber,
                SeatType = seat.Seat.SeatType.TypeName,
                Price = seat.Price,
                Status = status,
                IsMine = myBooking != null
                    && status == BookingConstants.ShowtimeSeatStatuses.Held
                    && seat.HeldByBookingId == myBooking.BookingId
            });
        }

        return model;
    }

    public Task<bool> IsShowtimeAvailableForUpdatesAsync(int showtimeId, CancellationToken ct = default)
        => showtimeRepository.ExistsForShowingMovieAsync(showtimeId, ct);

    private static MovieFilterViewModel NormalizeFilter(MovieFilterViewModel? filter)
    {
        if (filter == null) return new MovieFilterViewModel();

        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var ageRating = string.IsNullOrWhiteSpace(filter.AgeRating) ? null : filter.AgeRating.Trim();
        var genreId = filter.GenreId is > 0 ? filter.GenreId : null;
        var sortBy = AllowedSortOptions.Contains(filter.SortBy)
            ? filter.SortBy
            : BookingConstants.MovieSortOptions.TitleAsc;

        return new MovieFilterViewModel
        {
            Search = search,
            GenreId = genreId,
            AgeRating = ageRating,
            ShowDate = filter.ShowDate,
            SortBy = sortBy,
            OnlyWithShowtimes = filter.OnlyWithShowtimes
        };
    }

    private static MovieCardViewModel MapMovieCard(MovieCatalogItemDto item)
    {
        return new MovieCardViewModel
        {
            MovieId = item.MovieId,
            Title = item.Title,
            Description = item.Description,
            DurationMinutes = item.DurationMinutes,
            AgeRating = item.AgeRating,
            ReleaseDate = item.ReleaseDate,
            PosterUrl = item.PosterUrl,
            Genres = item.Genres,
            UpcomingShowtimeCount = item.UpcomingShowtimeCount
        };
    }

    private static MovieCardViewModel CreateMovieViewModel(Movie movie, int upcomingShowtimeCount)
    {
        return new MovieCardViewModel
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            Description = movie.Description,
            DurationMinutes = movie.DurationMinutes,
            AgeRating = movie.AgeRating,
            ReleaseDate = movie.ReleaseDate,
            PosterUrl = movie.PosterUrl,
            Genres = movie.MovieGenres
                .Where(mg => mg.Genre != null)
                .Select(mg => mg.Genre.GenreName)
                .OrderBy(name => name)
                .ToList(),
            UpcomingShowtimeCount = upcomingShowtimeCount
        };
    }

    private static string FormatShowDateLabel(DateOnly date, DateOnly todayVn)
    {
        if (date == todayVn) return $"Hôm nay ({date:dd/MM})";
        if (date == todayVn.AddDays(1)) return $"Ngày mai ({date:dd/MM})";

        var dayName = date.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            DayOfWeek.Sunday => "Chủ Nhật",
            _ => string.Empty
        };

        return $"{dayName} ({date:dd/MM})";
    }
}
