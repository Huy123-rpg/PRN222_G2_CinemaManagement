using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.Interfaces;

public interface IBookingRepository : IGenericRepository<Booking>
{
    Task<Booking?> GetActivePendingOnlineBookingAsync(int showtimeId, int userId, DateTime utcNow, CancellationToken ct = default);
    Task<Booking?> LockAndGetOwnedOnlineBookingAsync(int customerId, int bookingId, CancellationToken ct = default);
    Task<Booking?> GetByIdWithHeldSeatsAsync(int bookingId, CancellationToken ct = default);
    Task<int> UpdatePendingTotalAmountIfValidAsync(int bookingId, decimal totalAmount, DateTime utcNow, CancellationToken ct = default);
    Task<int> CancelPendingBookingAsync(int bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetExpiredPendingBookingsAsync(DateTime utcNow, int batchSize, CancellationToken ct = default);
    Task<int> MarkExpiredIfPendingAsync(int bookingId, DateTime utcNow, CancellationToken ct = default);
    Task<int> MarkConfirmedIfValidAsync(int bookingId, DateTime utcNow, CancellationToken ct = default);
    Task<Booking?> GetOwnedOnlineBookingDetailsAsync(int customerId, int bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetOwnedOnlineBookingsDetailsAsync(int customerId, CancellationToken ct = default);
    Task<Booking?> GetConfirmedOnlineBookingForConcessionQrAsync(int customerId, int bookingId, CancellationToken ct = default);
}

