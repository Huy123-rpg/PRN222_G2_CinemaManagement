using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

/// <summary>
/// Tầng đọc dữ liệu phục vụ màn hình phim, sơ đồ ghế và lịch sử đơn.
/// Tất cả thao tác ở đây là thao tác đọc thuần: không tạo đơn, không tự giữ ghế,
/// không thay đổi trạng thái ghế (đúng yêu cầu tiêu chí nghiệm thu A03).
/// </summary>
public sealed class BookingCatalogService(CinemaDbContext db, TimeProvider clock)
{
    /// <summary>Danh sách phim đang mở bán (trạng thái <c>Showing</c>).</summary>
    public async Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default)
    {
        return await db.Movies
            .AsNoTracking()
            .Where(m => m.Status == "Showing")
            .OrderBy(m => m.Title)
            .Select(m => new MovieCardViewModel(m.MovieId, m.Title, m.Description, m.DurationMinutes,
                m.AgeRating, m.PosterUrl))
            .ToListAsync(ct);
    }

    /// <summary>Chi tiết phim kèm các suất chiếu còn có thể đặt vé. Trả null nếu phim không mở bán.</summary>
    public async Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var movie = await db.Movies
            .AsNoTracking()
            .Where(m => m.MovieId == movieId && m.Status == "Showing")
            .Select(m => new MovieCardViewModel(m.MovieId, m.Title, m.Description, m.DurationMinutes,
                m.AgeRating, m.PosterUrl))
            .FirstOrDefaultAsync(ct);

        if (movie is null) return null;

        // Chỉ mở bán suất còn lịch, chưa bắt đầu và thuộc phòng đang hoạt động.
        var showtimes = await db.Showtimes
            .AsNoTracking()
            .Where(s => s.MovieId == movieId
                     && s.Status == "Scheduled"
                     && s.StartAt > now
                     && s.Auditorium.Status == "Active")
            .OrderBy(s => s.StartAt)
            .Select(s => new ShowtimeOptionViewModel(s.ShowtimeId, s.Auditorium.Name, s.StartAt, s.BasePrice))
            .ToListAsync(ct);

        return new MovieDetailsViewModel(movie, showtimes);
    }

    /// <summary>
    /// Sơ đồ ghế của một suất chiếu. Trả null nếu suất không còn nhận đặt vé.
    /// Tham số <paramref name="userId"/> dùng để đánh dấu ghế đang do chính người xem giữ.
    /// </summary>
    public async Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var showtime = await db.Showtimes
            .AsNoTracking()
            .Where(s => s.ShowtimeId == showtimeId
                     && s.Status == "Scheduled"
                     && s.StartAt > now
                     && s.Movie.Status == "Showing"
                     && s.Auditorium.Status == "Active")
            .Select(s => new { s.ShowtimeId, s.StartAt, MovieTitle = s.Movie.Title, Auditorium = s.Auditorium.Name })
            .FirstOrDefaultAsync(ct);

        if (showtime is null) return null;

        // Đơn đang giữ của chính người xem: dùng để biết ghế nào là "của mình" và hạn giữ còn lại.
        Booking? myBooking = null;
        if (userId is int uid && uid > 0)
        {
            myBooking = await db.Bookings
                .AsNoTracking()
                .Where(b => b.ShowtimeId == showtimeId
                         && b.Status == BookingStates.BookingPending
                         && b.ExpiresAt > now
                         && b.Customer != null && b.Customer.UserId == uid)
                .OrderByDescending(b => b.BookingId)
                .FirstOrDefaultAsync(ct);
        }

        var showSeats = await db.ShowtimeSeats
            .AsNoTracking()
            .Where(ss => ss.ShowtimeId == showtimeId)
            .OrderBy(ss => ss.Seat.RowLabel).ThenBy(ss => ss.Seat.SeatNumber)
            .Select(ss => new SeatViewModel(
                ss.ShowtimeSeatId,
                ss.Seat.RowLabel,
                ss.Seat.SeatNumber,
                ss.Seat.SeatType.TypeName,
                ss.Price,
                ss.Status,
                myBooking != null && ss.HeldByBookingId == myBooking.BookingId))
            .ToListAsync(ct);

        return new SeatMapViewModel(showtime.ShowtimeId, showtime.MovieTitle, showtime.Auditorium,
            showtime.StartAt, showSeats, myBooking?.BookingId, myBooking?.ExpiresAt, now);
    }

    /// <summary>Chi tiết một đơn. Trả null nếu không tồn tại hoặc không thuộc về người dùng.</summary>
    public Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default) =>
        LoadBookingDetailsAsync(b => b.BookingId == bookingId && b.Customer != null && b.Customer.UserId == userId, ct);

    /// <summary>Danh sách đơn của một khách hàng, mới nhất trước.</summary>
    public async Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default)
    {
        var bookings = await LoadBookingsAsync(b => b.Customer != null && b.Customer.UserId == userId, ct);
        return bookings
            .OrderByDescending(b => b.Showtime.StartAt)
            .ThenByDescending(b => b.BookingId)
            .Select(MapBooking)
            .ToList();
    }

    // ------------------------------------------------------------------
    // Nội bộ
    // ------------------------------------------------------------------

    /// <summary>
    /// Tải danh sách đơn kèm quan hệ cần thiết. Dùng projection để không theo dõi thực thể,
    /// tránh ràng buộc tracker với các câu UPDATE nguyên tử ở tầng nghiệp vụ.
    /// </summary>
    private async Task<List<Booking>> LoadBookingsAsync(System.Linq.Expressions.Expression<Func<Booking, bool>> predicate,
        CancellationToken ct)
    {
        return await db.Bookings
            .AsNoTracking()
            .Include(b => b.Showtime).ThenInclude(s => s.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s.Auditorium)
            .Include(b => b.Customer).ThenInclude(c => c!.User)
            .Include(b => b.HeldShowtimeSeats).ThenInclude(ss => ss.Seat).ThenInclude(s => s.SeatType)
            .Include(b => b.Tickets).ThenInclude(t => t.ShowtimeSeat).ThenInclude(ss => ss.Seat).ThenInclude(s => s.SeatType)
            .Where(predicate)
            .ToListAsync(ct);
    }

    private async Task<BookingDetailsViewModel?> LoadBookingDetailsAsync(
        System.Linq.Expressions.Expression<Func<Booking, bool>> predicate, CancellationToken ct)
    {
        var booking = (await LoadBookingsAsync(predicate, ct)).FirstOrDefault();
        return booking is null ? null : MapBooking(booking);
    }

    /// <summary>Chuyển đơn sang view model, kèm tổng tiền và danh sách ghế/vé.</summary>
    private BookingDetailsViewModel MapBooking(Booking booking)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // Vé đã phát là nguồn hiển thị chính xác ghế của đơn đã xác nhận;
        // với đơn đang giữ thì lấy từ các ghế đang được giữ.
        var seats = booking.Tickets.Count > 0
            ? booking.Tickets.Select(t => new BookingSeatViewModel(t.ShowtimeSeatId,
                    $"{t.ShowtimeSeat.Seat.RowLabel}{t.ShowtimeSeat.Seat.SeatNumber}",
                    t.ShowtimeSeat.Seat.SeatType.TypeName, t.Price)).ToList()
            : booking.HeldShowtimeSeats.Select(ss => new BookingSeatViewModel(ss.ShowtimeSeatId,
                    $"{ss.Seat.RowLabel}{ss.Seat.SeatNumber}",
                    ss.Seat.SeatType.TypeName, ss.Price)).ToList();

        var tickets = booking.Tickets
            .Select(t => new TicketViewModel(t.TicketId, t.TicketCode,
                $"{t.ShowtimeSeat.Seat.RowLabel}{t.ShowtimeSeat.Seat.SeatNumber}", t.Price, t.Status))
            .ToList();

        var customer = booking.Customer;

        return new BookingDetailsViewModel(
            booking.BookingId,
            booking.BookingCode,
            booking.ShowtimeId,
            booking.Showtime.Movie.Title,
            booking.Showtime.Auditorium.Name,
            booking.Showtime.StartAt,
            booking.Status,
            booking.ExpiresAt,
            now,
            booking.TotalAmount,
            customer?.User?.FullName ?? customer?.User?.Username ?? "Khách hàng",
            customer?.User?.Email ?? string.Empty,
            seats,
            tickets);
    }
}
