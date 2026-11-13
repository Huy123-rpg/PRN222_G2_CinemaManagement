using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.ViewModels;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>Facade điều phối các service nghiệp vụ đặt vé cho controller và background worker.</summary>
public class OnlineBookingService(
    IMovieCatalogService movies,
    ISeatBookingService seats,
    IBookingConcessionService concessions,
    IBookingPaymentService payments,
    IBookingTicketService tickets) : IOnlineBookingService
{
    public Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default)
        => movies.GetMoviesAsync(ct);

    public Task<MovieCatalogViewModel> GetMovieCatalogAsync(MovieFilterViewModel? filter, CancellationToken ct = default)
        => movies.GetMovieCatalogAsync(filter, ct);

    public Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default)
        => movies.GetMovieAsync(movieId, ct);

    public Task<SeatMapViewModel?> GetSeatMapAsync(int screeningId, int? userId, CancellationToken ct = default)
        => movies.GetSeatMapAsync(screeningId, userId, ct);

    public Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default)
        => seats.SetSeatsAsync(userId, request, ct);

    public Task<ConcessionSelectionViewModel?> GetConcessionSelectionAsync(int userId, int bookingId, CancellationToken ct = default)
        => concessions.GetSelectionPageAsync(userId, bookingId, ct);

    public Task<ConcessionSelectionResultViewModel> SetConcessionsAsync(int userId, SetConcessionsRequest request, CancellationToken ct = default)
        => concessions.SaveSelectionAsync(userId, request, ct);

    public Task CancelAsync(int userId, int bookingId, CancellationToken ct = default)
        => seats.CancelAsync(userId, bookingId, ct);

    public Task<int> ReleaseExpiredAsync(CancellationToken ct = default)
        => seats.ReleaseExpiredAsync(ct);

    public Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default)
        => payments.PayAsync(userId, request, ct);

    public Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct = default)
        => payments.ConfirmPaymentAsync(bookingId, isSuccess, ct);

    public Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default)
        => tickets.GetBookingAsync(userId, bookingId, ct);

    public Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default)
        => tickets.GetMyBookingsAsync(userId, ct);

    public Task<MyBookingsViewModel> GetMyBookingsPagedAsync(
        int userId,
        int page = 1,
        int pageSize = BookingConstants.DefaultBookingPageSize,
        CancellationToken ct = default)
        => tickets.GetMyBookingsPagedAsync(userId, page, pageSize, ct);

    public Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default)
        => tickets.GetTicketQrAsync(userId, ticketId, ct);

    public Task<byte[]?> GetConcessionQrAsync(int userId, int bookingId, CancellationToken ct = default)
        => tickets.GetConcessionQrAsync(userId, bookingId, ct);
}

