using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface IScreeningSeatRepository : IGenericRepository<ScreeningSeat>
{
    Task<IReadOnlyList<ScreeningSeat>> GetByScreeningIdWithDetailsAsync(int screeningId, CancellationToken ct = default);
    Task<IReadOnlyList<ScreeningSeat>> GetActiveSeatsForScreeningAsync(int screeningId, int[] seatIds, CancellationToken ct = default);
    Task<IReadOnlyList<ScreeningSeat>> GetHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<int> TryHoldSeatAsync(int screeningSeatId, int bookingId, DateTime expiresAt, DateTime utcNow, CancellationToken ct = default);
    Task<int> ReleaseUnselectedHeldSeatsAsync(int bookingId, int[] keepSeatIds, CancellationToken ct = default);
    Task<int> ReleaseHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<int> MarkSeatSoldFromHeldAsync(int screeningSeatId, int bookingId, DateTime? requireNotExpiredUtc, bool requireActiveSeat, CancellationToken ct = default);
}

