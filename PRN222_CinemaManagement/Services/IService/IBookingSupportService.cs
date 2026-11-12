using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Services.IService;

public interface IBookingSupportService
{
    Task<int> GetActiveCustomerIdAsync(int userId, CancellationToken ct = default);
    Task<Screening?> GetOpenScreeningAsync(int screeningId, CancellationToken ct = default);
    Task<Booking> LockOwnedBookingAsync(int customerId, int bookingId, CancellationToken ct = default);
    Task ReleaseHeldSeatsAsync(int bookingId, CancellationToken ct = default);
    Task NotifySeatsChangedAsync(int screeningId);
    void EnsurePendingBooking(Booking booking);
    DateTime AsUtc(DateTime value);
}

