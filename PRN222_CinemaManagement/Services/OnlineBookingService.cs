using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Services.OnlineBooking;

namespace PRN222_CinemaManagement.Services;

// Đầu mối cho controller và worker. Nghiệp vụ nằm trong từng service bên dưới.
// Các service được DI tạo trong cùng scope nên dùng chung một CinemaDbContext.
public class OnlineBookingService(MovieCatalogService movies, SeatBookingService seats,
    BookingPaymentService payments, BookingTicketService tickets) : IOnlineBookingService
{
    // Xem phim, suất chiếu và sơ đồ ghế.
    public Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default)
    {
        return movies.GetMoviesAsync(ct);
    }

    public Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default)
    {
        return movies.GetMovieAsync(movieId, ct);
    }

    public Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default)
    {
        return movies.GetSeatMapAsync(showtimeId, userId, ct);
    }

    // Giữ ghế, hủy đơn và giải phóng hết hạn.
    public Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default)
    {
        return seats.SetSeatsAsync(userId, request, ct);
    }

    public Task CancelAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        return seats.CancelAsync(userId, bookingId, ct);
    }

    public Task<int> ReleaseExpiredAsync(CancellationToken ct = default)
    {
        return seats.ReleaseExpiredAsync(ct);
    }

    // Thanh toán giả lập và xác nhận kết quả VNPay.
    public Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default)
    {
        return payments.PayAsync(userId, request, ct);
    }

    public Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct)
    {
        return payments.ConfirmPaymentAsync(bookingId, isSuccess, ct);
    }

    // Xem đơn, danh sách vé và ảnh QR.
    public Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        return tickets.GetBookingAsync(userId, bookingId, ct);
    }

    public Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default)
    {
        return tickets.GetMyBookingsAsync(userId, ct);
    }

    public Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default)
    {
        return tickets.GetTicketQrAsync(userId, ticketId, ct);
    }
}
