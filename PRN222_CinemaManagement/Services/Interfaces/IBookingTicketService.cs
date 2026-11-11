using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.Interfaces;

public interface IBookingTicketService
{
    Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default);
    Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default);
    Task<byte[]?> GetConcessionQrAsync(int userId, int bookingId, CancellationToken ct = default);
}

