using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.Interfaces;

public interface IBookingPaymentService
{
    Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default);
    Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct = default);
}

