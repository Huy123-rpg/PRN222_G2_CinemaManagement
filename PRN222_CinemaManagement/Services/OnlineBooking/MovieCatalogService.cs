using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

// Xem phim, suất chiếu và sơ đồ ghế. Các hàm ở đây chỉ đọc dữ liệu.
public class MovieCatalogService(CinemaDbContext context, BookingSupportService support)
{
    // Danh sách phim đang mở bán, dùng cho trang Movies/Index.
    public async Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default)
    {
        var movies = await context.Movies.AsNoTracking()
            .Where(m => m.Status == "Showing").OrderBy(m => m.Title).ToListAsync(ct);
        var result = new List<MovieCardViewModel>();
        foreach (var movie in movies)
        {
            result.Add(CreateMovieViewModel(movie));
        }
        return result;
    }

    // Chi tiết một phim và các suất chiếu chưa bắt đầu.
    public async Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default)
    {
        var movie = await context.Movies.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MovieId == movieId && m.Status == "Showing", ct);
        if (movie == null) return null;

        var now = DateTime.UtcNow;
        var showtimes = await context.Showtimes.AsNoTracking()
            .Include(s => s.Auditorium)
            .Where(s => s.MovieId == movieId && s.Status == "Scheduled"
                && s.StartAt > now && s.Auditorium.Status == "Active")
            .OrderBy(s => s.StartAt).ToListAsync(ct);

        var model = new MovieDetailsViewModel { Movie = CreateMovieViewModel(movie) };
        foreach (var showtime in showtimes)
        {
            model.Showtimes.Add(new ShowtimeOptionViewModel
            {
                ShowtimeId = showtime.ShowtimeId,
                Auditorium = showtime.Auditorium.Name,
                StartAt = BookingSupportService.AsUtc(showtime.StartAt),
                BasePrice = showtime.BasePrice
            });
        }
        return model;
    }

    // Sơ đồ ghế mới nhất; userId chỉ dùng để đánh dấu các ghế của chính người xem.
    public async Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default)
    {
        var showtime = await support.GetOpenShowtimeAsync(showtimeId, ct);
        if (showtime == null) return null;

        var now = DateTime.UtcNow;
        Booking? myBooking = null;
        if (userId.HasValue)
        {
            // Chỉ trả mã đơn của người xem, không gửi người giữ ghế khác ra trình duyệt.
            myBooking = await context.Bookings.AsNoTracking()
                .Where(b => b.ShowtimeId == showtimeId && b.BookingChannel == "Online"
                    && b.Status == "Pending" && b.ExpiresAt > now && b.Customer != null
                    && b.Customer.UserId == userId.Value && b.Customer.User.Status == "Active"
                    && b.Customer.User.Role.RoleName == "Customer")
                .OrderByDescending(b => b.BookingId).FirstOrDefaultAsync(ct);
        }

        var showtimeSeats = await context.ShowtimeSeats.AsNoTracking()
            .Include(s => s.Seat).ThenInclude(s => s.SeatType)
            .Where(s => s.ShowtimeId == showtimeId)
            .OrderBy(s => s.Seat.RowLabel).ThenBy(s => s.Seat.SeatNumber).ToListAsync(ct);

        var model = new SeatMapViewModel
        {
            ShowtimeId = showtimeId,
            MovieTitle = showtime.Movie.Title,
            Auditorium = showtime.Auditorium.Name,
            StartAt = BookingSupportService.AsUtc(showtime.StartAt),
            ServerNow = now
        };
        if (myBooking != null)
        {
            model.BookingId = myBooking.BookingId;
            model.ExpiresAt = BookingSupportService.AsUtc(myBooking.ExpiresAt);
        }

        foreach (var seat in showtimeSeats)
        {
            var status = seat.Status;
            if (status != "Sold")
            {
                if (seat.Seat.Status != "Active" || !seat.Seat.SeatType.IsActive)
                    status = "Blocked";
                else if (status == "Held" && seat.HoldExpiresAt <= now)
                    status = "Available"; // Hết hạn thì có thể chọn lại ngay, không cần chờ worker.
            }
            model.Seats.Add(new SeatViewModel
            {
                ShowtimeSeatId = seat.ShowtimeSeatId,
                RowLabel = seat.Seat.RowLabel,
                SeatNumber = seat.Seat.SeatNumber,
                SeatType = seat.Seat.SeatType.TypeName,
                Price = seat.Price,
                Status = status,
                IsMine = myBooking != null && status == "Held"
                    && seat.HeldByBookingId == myBooking.BookingId
            });
        }
        return model;
    }

    private static MovieCardViewModel CreateMovieViewModel(Movie movie)
    {
        return new MovieCardViewModel
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            Description = movie.Description,
            DurationMinutes = movie.DurationMinutes,
            AgeRating = movie.AgeRating,
            PosterUrl = movie.PosterUrl
        };
    }
}
