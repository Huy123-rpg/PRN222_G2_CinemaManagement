using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services;

/// <summary>Điểm vào nghiệp vụ đặt vé; userId phải lấy từ Session, không lấy từ request.</summary>
public interface IOnlineBookingService
{
    Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default);
    Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default);
    Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default);
    Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default);
    Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default);
    Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default);
    Task CancelAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<int> ReleaseExpiredAsync(CancellationToken ct = default);
    Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default);
}

/// <summary>Lỗi nghiệp vụ có thể hiển thị trực tiếp bằng tiếng Việt; không lộ lỗi SQL ra ngoài.</summary>
public class BookingException(string message, int statusCode = 409) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
