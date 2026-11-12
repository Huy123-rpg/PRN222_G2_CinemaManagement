using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public interface IBookingPaymentService
{
    Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default);
    Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct = default);
}

