using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.Interfaces;

/// <summary>Điểm vào nghiệp vụ đặt vé; userId phải lấy từ Session, không lấy từ request.</summary>
public interface IOnlineBookingService
{
    Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default);
    Task<MovieCatalogViewModel> GetMovieCatalogAsync(MovieFilterViewModel? filter, CancellationToken ct = default);
    Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default);
    Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default);
    Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default);
    Task<ConcessionSelectionViewModel?> GetConcessionSelectionAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<ConcessionSelectionResultViewModel> SetConcessionsAsync(int userId, SetConcessionsRequest request, CancellationToken ct = default);
    Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default);
    Task<MyBookingsViewModel> GetMyBookingsPagedAsync(
        int userId,
        int page = 1,
        int pageSize = BookingConstants.DefaultBookingPageSize,
        CancellationToken ct = default);
    Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default);
    Task CancelAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<int> ReleaseExpiredAsync(CancellationToken ct = default);
    Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default);
    Task<byte[]?> GetConcessionQrAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct = default);
}

