using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public interface ISeatBookingService
{
    Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default);
    Task CancelAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<int> ReleaseExpiredAsync(CancellationToken ct = default);
}

