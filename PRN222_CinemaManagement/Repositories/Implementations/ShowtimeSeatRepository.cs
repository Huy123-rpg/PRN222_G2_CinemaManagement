using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.Interfaces;

namespace PRN222_CinemaManagement.Repositories.Implementations;

public class ShowtimeSeatRepository(CinemaDbContext context)
    : GenericRepository<ShowtimeSeat>(context), IShowtimeSeatRepository
{
    public async Task<IReadOnlyList<ShowtimeSeat>> GetByShowtimeIdWithDetailsAsync(int showtimeId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(s => s.Seat)
            .ThenInclude(s => s.SeatType)
            .Where(s => s.ShowtimeId == showtimeId)
            .OrderBy(s => s.Seat.RowLabel)
            .ThenBy(s => s.Seat.SeatNumber)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ShowtimeSeat>> GetActiveSeatsForShowtimeAsync(int showtimeId, int[] seatIds, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.ShowtimeId == showtimeId
                && seatIds.Contains(s.ShowtimeSeatId)
                && s.Seat.Status == BookingConstants.PhysicalSeatStatuses.Active
                && s.Seat.SeatType.IsActive)
            .OrderBy(s => s.ShowtimeSeatId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ShowtimeSeat>> GetHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.HeldByBookingId == bookingId
                && s.Status == BookingConstants.ShowtimeSeatStatuses.Held)
            .OrderBy(s => s.ShowtimeSeatId)
            .ToListAsync(ct);
    }

    public Task<int> TryHoldSeatAsync(int showtimeSeatId, int bookingId, DateTime expiresAt, DateTime utcNow, CancellationToken ct = default)
    {
        // Điều kiện và UPDATE chạy nguyên tử trên SQL Server để hai request không thể giữ cùng một ghế.
        return DbSet
            .Where(s => s.ShowtimeSeatId == showtimeSeatId
                && s.Seat.Status == BookingConstants.PhysicalSeatStatuses.Active
                && s.Seat.SeatType.IsActive
                && (s.Status == BookingConstants.ShowtimeSeatStatuses.Available
                    || (s.Status == BookingConstants.ShowtimeSeatStatuses.Held
                        && (s.HoldExpiresAt <= utcNow || s.HeldByBookingId == bookingId))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingConstants.ShowtimeSeatStatuses.Held)
                .SetProperty(s => s.HeldByBookingId, (int?)bookingId)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)expiresAt), ct);
    }

    public Task<int> ReleaseUnselectedHeldSeatsAsync(int bookingId, int[] keepSeatIds, CancellationToken ct = default)
    {
        return DbSet
            .Where(s => s.HeldByBookingId == bookingId
                && s.Status == BookingConstants.ShowtimeSeatStatuses.Held
                && !keepSeatIds.Contains(s.ShowtimeSeatId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingConstants.ShowtimeSeatStatuses.Available)
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }

    public Task<int> ReleaseHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        // Không nhả ghế Sold hoặc ghế hết hạn đã được đơn khác giữ lại.
        return DbSet
            .Where(s => s.HeldByBookingId == bookingId
                && s.Status == BookingConstants.ShowtimeSeatStatuses.Held)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingConstants.ShowtimeSeatStatuses.Available)
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }

    public Task<int> MarkSeatSoldFromHeldAsync(
        int showtimeSeatId,
        int bookingId,
        DateTime? requireNotExpiredUtc,
        bool requireActiveSeat,
        CancellationToken ct = default)
    {
        var query = DbSet.Where(s => s.ShowtimeSeatId == showtimeSeatId
            && s.Status == BookingConstants.ShowtimeSeatStatuses.Held
            && s.HeldByBookingId == bookingId);

        if (requireNotExpiredUtc.HasValue)
        {
            var checkTime = requireNotExpiredUtc.Value;
            query = query.Where(s => s.HoldExpiresAt > checkTime);
        }

        if (requireActiveSeat)
        {
            query = query.Where(s => s.Seat.Status == BookingConstants.PhysicalSeatStatuses.Active
                && s.Seat.SeatType.IsActive);
        }

        return query.ExecuteUpdateAsync(setters => setters
            .SetProperty(s => s.Status, BookingConstants.ShowtimeSeatStatuses.Sold)
            .SetProperty(s => s.HeldByBookingId, (int?)null)
            .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }
}

