using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.Interfaces;

namespace PRN222_CinemaManagement.Repositories.Implementations;

public class BookingRepository(CinemaDbContext context)
    : GenericRepository<Booking>(context), IBookingRepository
{
    public Task<Booking?> GetActivePendingOnlineBookingAsync(int showtimeId, int userId, DateTime utcNow, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .Where(b => b.ShowtimeId == showtimeId
                && b.BookingChannel == BookingConstants.Channels.Online
                && b.Status == BookingConstants.BookingStatuses.Pending
                && b.ExpiresAt > utcNow
                && b.Customer != null
                && b.Customer.UserId == userId
                && b.Customer.User.Status == BookingConstants.ActiveUserStatus
                && b.Customer.User.Role.RoleName == BookingConstants.CustomerRole)
            .OrderByDescending(b => b.BookingId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Booking?> LockAndGetOwnedOnlineBookingAsync(int customerId, int bookingId, CancellationToken ct = default)
    {
        // UPDATE giữ nguyên Pending để khóa dòng đơn trong SQL transaction hiện tại đến khi commit/rollback.
        await DbSet
            .Where(b => b.BookingId == bookingId
                && b.CustomerId == customerId
                && b.BookingChannel == BookingConstants.Channels.Online
                && b.Status == BookingConstants.BookingStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, BookingConstants.BookingStatuses.Pending), ct);

        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId
                && b.CustomerId == customerId
                && b.BookingChannel == BookingConstants.Channels.Online, ct);
    }

    public Task<Booking?> GetByIdWithHeldSeatsAsync(int bookingId, CancellationToken ct = default)
    {
        return DbSet
            .Include(b => b.HeldShowtimeSeats)
            .Include(b => b.BookingConcessions)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);
    }

    public Task<int> UpdatePendingTotalAmountIfValidAsync(int bookingId, decimal totalAmount, DateTime utcNow, CancellationToken ct = default)
    {
        return DbSet
            .Where(b => b.BookingId == bookingId
                && b.Status == BookingConstants.BookingStatuses.Pending
                && b.ExpiresAt > utcNow
                && b.Showtime.Status == BookingConstants.ShowtimeStatuses.Scheduled
                && b.Showtime.StartAt > utcNow
                && b.Showtime.Movie.Status == BookingConstants.MovieStatuses.Showing
                && b.Showtime.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.TotalAmount, totalAmount), ct);
    }

    public Task<int> CancelPendingBookingAsync(int bookingId, CancellationToken ct = default)
    {
        return DbSet
            .Where(b => b.BookingId == bookingId && b.Status == BookingConstants.BookingStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, BookingConstants.BookingStatuses.Cancelled), ct);
    }

    public async Task<IReadOnlyList<Booking>> GetExpiredPendingBookingsAsync(DateTime utcNow, int batchSize, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(b => b.Status == BookingConstants.BookingStatuses.Pending && b.ExpiresAt <= utcNow)
            .OrderBy(b => b.BookingId)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public Task<int> MarkExpiredIfPendingAsync(int bookingId, DateTime utcNow, CancellationToken ct = default)
    {
        return DbSet
            .Where(b => b.BookingId == bookingId
                && b.Status == BookingConstants.BookingStatuses.Pending
                && b.ExpiresAt <= utcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, BookingConstants.BookingStatuses.Expired), ct);
    }

    public Task<int> MarkConfirmedIfValidAsync(int bookingId, DateTime utcNow, CancellationToken ct = default)
    {
        return DbSet
            .Where(b => b.BookingId == bookingId
                && b.Status == BookingConstants.BookingStatuses.Pending
                && b.ExpiresAt > utcNow
                && b.Showtime.Status == BookingConstants.ShowtimeStatuses.Scheduled
                && b.Showtime.StartAt > utcNow
                && b.Showtime.Movie.Status == BookingConstants.MovieStatuses.Showing
                && b.Showtime.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, BookingConstants.BookingStatuses.Confirmed), ct);
    }

    public Task<Booking?> GetOwnedOnlineBookingDetailsAsync(int customerId, int bookingId, CancellationToken ct = default)
    {
        return BuildDetailsQuery(customerId)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);
    }

    public async Task<IReadOnlyList<Booking>> GetOwnedOnlineBookingsDetailsAsync(int customerId, CancellationToken ct = default)
    {
        return await BuildDetailsQuery(customerId)
            .OrderByDescending(b => b.Showtime.StartAt)
            .ThenByDescending(b => b.BookingId)
            .ToListAsync(ct);
    }

    public Task<Booking?> GetConfirmedOnlineBookingForConcessionQrAsync(int customerId, int bookingId, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId
                && b.CustomerId == customerId
                && b.BookingChannel == BookingConstants.Channels.Online
                && b.Status == BookingConstants.BookingStatuses.Confirmed
                && b.BookingConcessions.Any(), ct);
    }

    private IQueryable<Booking> BuildDetailsQuery(int customerId)
    {
        return DbSet
            .AsNoTracking()
            .AsSplitQuery()
            .Where(b => b.CustomerId == customerId && b.BookingChannel == BookingConstants.Channels.Online)
            .Include(b => b.Customer).ThenInclude(c => c!.User)
            .Include(b => b.Showtime).ThenInclude(s => s.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s.Auditorium)
            .Include(b => b.HeldShowtimeSeats).ThenInclude(s => s.Seat).ThenInclude(s => s.SeatType)
            .Include(b => b.BookingConcessions).ThenInclude(bc => bc.ConcessionItem)
            .Include(b => b.Tickets).ThenInclude(t => t.ShowtimeSeat).ThenInclude(s => s.Seat).ThenInclude(s => s.SeatType);
    }
}

