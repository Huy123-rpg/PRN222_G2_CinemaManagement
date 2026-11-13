using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Models.DTOs;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.ViewModels;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>Xem phim, tìm kiếm/lọc phim, suất chiếu và sơ đồ ghế. Các phương thức chỉ đọc dữ liệu.</summary>
public class MovieCatalogService(
    IMovieCatalogRepository movieRepository,
    IScreeningBookingRepository screeningRepository,
    IScreeningSeatRepository screeningSeatRepository,
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
        BookingConstants.MovieSortOptions.ScreeningsDesc
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
            OnlyWithScreenings = normalizedFilter.OnlyWithScreenings
        };

        // Tuần tự trên cùng một scoped DbContext.
        var options = await movieRepository.GetShowingFilterOptionsAsync(now, ct);
        var items = await movieRepository.SearchShowingMoviesAsync(criteria, now, ct);

        var totalFiltered = items.Count;
        var pageSize = normalizedFilter.PageSize;
        var totalPages = totalFiltered <= 0 ? 1 : (int)Math.Ceiling((double)totalFiltered / pageSize);
        var currentPage = Math.Clamp(normalizedFilter.Page, 1, totalPages);
        normalizedFilter.Page = currentPage;

        var pagedMovies = items
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(MapMovieCard)
            .ToList();

        var todayVn = DateOnly.FromDateTime(BookingDisplay.VietnamTime(now));

        return new MovieCatalogViewModel
        {
            Filter = normalizedFilter,
            Pagination = new PaginationViewModel
            {
                Page = currentPage,
                PageSize = pageSize,
                TotalItems = totalFiltered
            },
            TotalShowingCount = options.TotalShowingCount,
            Movies = pagedMovies,
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

        var screenings = await screeningRepository.GetUpcomingByMovieIdAsync(movieId, DateTime.UtcNow, ct);
        var model = new MovieDetailsViewModel { Movie = CreateMovieViewModel(movie, screenings.Count) };

        foreach (var screening in screenings)
        {
            model.Screenings.Add(new ScreeningOptionViewModel
            {
                ScreeningId = screening.ScreeningId,
                Auditorium = screening.Auditorium.Name,
                StartAt = support.AsUtc(screening.StartAt),
                BasePrice = screening.BasePrice
            });
        }

        return model;
    }

    public async Task<SeatMapViewModel?> GetSeatMapAsync(int screeningId, int? userId, CancellationToken ct = default)
    {
        var screening = await support.GetOpenScreeningAsync(screeningId, ct);
        if (screening == null) return null;

        var now = DateTime.UtcNow;
        Booking? myBooking = null;
        if (userId.HasValue)
        {
            // Chỉ trả mã đơn của người xem, không gửi người giữ ghế khác ra trình duyệt.
            myBooking = await bookingRepository.GetActivePendingOnlineBookingAsync(screeningId, userId.Value, now, ct);
        }

        var screeningSeats = await screeningSeatRepository.GetByScreeningIdWithDetailsAsync(screeningId, ct);

        var model = new SeatMapViewModel
        {
            ScreeningId = screeningId,
            MovieTitle = screening.Movie.Title,
            Auditorium = screening.Auditorium.Name,
            StartAt = support.AsUtc(screening.StartAt),
            ServerNow = now
        };

        if (myBooking != null)
        {
            model.BookingId = myBooking.BookingId;
            model.ExpiresAt = support.AsUtc(myBooking.ExpiresAt);
        }

        foreach (var seat in screeningSeats)
        {
            var status = seat.Status;
            if (status != BookingConstants.ScreeningSeatStatuses.Sold)
            {
                if (seat.Seat.Status != BookingConstants.PhysicalSeatStatuses.Active || !seat.Seat.SeatType.IsActive)
                {
                    status = BookingConstants.ScreeningSeatStatuses.Blocked;
                }
                else if (status == BookingConstants.ScreeningSeatStatuses.Held && seat.HoldExpiresAt <= now)
                {
                    // Hết hạn thì có thể chọn lại ngay, không cần chờ worker.
                    status = BookingConstants.ScreeningSeatStatuses.Available;
                }
            }

            model.Seats.Add(new SeatViewModel
            {
                ScreeningSeatId = seat.ScreeningSeatId,
                RowLabel = seat.Seat.RowLabel,
                SeatNumber = seat.Seat.SeatNumber,
                SeatType = seat.Seat.SeatType.TypeName,
                Price = seat.Price,
                Status = status,
                IsMine = myBooking != null
                    && status == BookingConstants.ScreeningSeatStatuses.Held
                    && seat.HeldByBookingId == myBooking.BookingId
            });
        }

        return model;
    }

    public Task<bool> IsScreeningAvailableForUpdatesAsync(int screeningId, CancellationToken ct = default)
        => screeningRepository.ExistsForShowingMovieAsync(screeningId, ct);

    private static MovieFilterViewModel NormalizeFilter(MovieFilterViewModel? filter)
    {
        if (filter == null) return new MovieFilterViewModel();

        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var ageRating = string.IsNullOrWhiteSpace(filter.AgeRating) ? null : filter.AgeRating.Trim();
        var genreId = filter.GenreId is > 0 ? filter.GenreId : null;
        var sortBy = AllowedSortOptions.Contains(filter.SortBy)
            ? filter.SortBy
            : BookingConstants.MovieSortOptions.TitleAsc;
        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0
            ? Math.Clamp(filter.PageSize, 1, BookingConstants.MaxPageSize)
            : BookingConstants.DefaultMoviePageSize;

        return new MovieFilterViewModel
        {
            Search = search,
            GenreId = genreId,
            AgeRating = ageRating,
            ShowDate = filter.ShowDate,
            SortBy = sortBy,
            OnlyWithScreenings = filter.OnlyWithScreenings,
            Page = page,
            PageSize = pageSize
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
            UpcomingScreeningCount = item.UpcomingScreeningCount
        };
    }

    private static MovieCardViewModel CreateMovieViewModel(Movie movie, int upcomingScreeningCount)
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
            UpcomingScreeningCount = upcomingScreeningCount
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
