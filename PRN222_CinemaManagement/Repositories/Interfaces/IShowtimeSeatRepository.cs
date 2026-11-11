using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.Interfaces;

public interface IShowtimeSeatRepository : IGenericRepository<ShowtimeSeat>
{
    Task<IReadOnlyList<ShowtimeSeat>> GetByShowtimeIdWithDetailsAsync(int showtimeId, CancellationToken ct = default);
    Task<IReadOnlyList<ShowtimeSeat>> GetActiveSeatsForShowtimeAsync(int showtimeId, int[] seatIds, CancellationToken ct = default);
    Task<IReadOnlyList<ShowtimeSeat>> GetHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<int> TryHoldSeatAsync(int showtimeSeatId, int bookingId, DateTime expiresAt, DateTime utcNow, CancellationToken ct = default);
    Task<int> ReleaseUnselectedHeldSeatsAsync(int bookingId, int[] keepSeatIds, CancellationToken ct = default);
    Task<int> ReleaseHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<int> MarkSeatSoldFromHeldAsync(int showtimeSeatId, int bookingId, DateTime? requireNotExpiredUtc, bool requireActiveSeat, CancellationToken ct = default);
}

