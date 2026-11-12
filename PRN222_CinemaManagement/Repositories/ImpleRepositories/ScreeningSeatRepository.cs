using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class ScreeningSeatRepository(CinemaDbContext context)
    : GenericRepository<ScreeningSeat>(context), IScreeningSeatRepository
{
    public async Task<IReadOnlyList<ScreeningSeat>> GetByScreeningIdWithDetailsAsync(int screeningId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(s => s.Seat)
            .ThenInclude(s => s.SeatType)
            .Where(s => s.ScreeningId == screeningId)
            .OrderBy(s => s.Seat.RowLabel)
            .ThenBy(s => s.Seat.SeatNumber)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScreeningSeat>> GetActiveSeatsForScreeningAsync(int screeningId, int[] seatIds, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.ScreeningId == screeningId
                && seatIds.Contains(s.ScreeningSeatId)
                && s.Seat.Status == BookingConstants.PhysicalSeatStatuses.Active
                && s.Seat.SeatType.IsActive)
            .OrderBy(s => s.ScreeningSeatId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScreeningSeat>> GetHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.HeldByBookingId == bookingId
                && s.Status == BookingConstants.ScreeningSeatStatuses.Held)
            .OrderBy(s => s.ScreeningSeatId)
            .ToListAsync(ct);
    }

    public Task<int> TryHoldSeatAsync(int screeningSeatId, int bookingId, DateTime expiresAt, DateTime utcNow, CancellationToken ct = default)
    {
        // Điều kiện và UPDATE chạy nguyên tử trên SQL Server để hai request không thể giữ cùng một ghế.
        return DbSet
            .Where(s => s.ScreeningSeatId == screeningSeatId
                && s.Seat.Status == BookingConstants.PhysicalSeatStatuses.Active
                && s.Seat.SeatType.IsActive
                && (s.Status == BookingConstants.ScreeningSeatStatuses.Available
                    || (s.Status == BookingConstants.ScreeningSeatStatuses.Held
                        && (s.HoldExpiresAt <= utcNow || s.HeldByBookingId == bookingId))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Held)
                .SetProperty(s => s.HeldByBookingId, (int?)bookingId)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)expiresAt), ct);
    }

    public Task<int> ReleaseUnselectedHeldSeatsAsync(int bookingId, int[] keepSeatIds, CancellationToken ct = default)
    {
        return DbSet
            .Where(s => s.HeldByBookingId == bookingId
                && s.Status == BookingConstants.ScreeningSeatStatuses.Held
                && !keepSeatIds.Contains(s.ScreeningSeatId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Available)
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }

    public Task<int> ReleaseHeldSeatsByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        // Không nhả ghế Sold hoặc ghế hết hạn đã được đơn khác giữ lại.
        return DbSet
            .Where(s => s.HeldByBookingId == bookingId
                && s.Status == BookingConstants.ScreeningSeatStatuses.Held)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Available)
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }

    public Task<int> MarkSeatSoldFromHeldAsync(
        int screeningSeatId,
        int bookingId,
        DateTime? requireNotExpiredUtc,
        bool requireActiveSeat,
        CancellationToken ct = default)
    {
        var query = DbSet.Where(s => s.ScreeningSeatId == screeningSeatId
            && s.Status == BookingConstants.ScreeningSeatStatuses.Held
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
            .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Sold)
            .SetProperty(s => s.HeldByBookingId, (int?)null)
            .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }
}

