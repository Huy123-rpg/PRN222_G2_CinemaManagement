using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public sealed class ScreeningRepository(CinemaDbContext db) : IScreeningRepository
{
    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<Movie?> GetMovieAsync(int movieId, CancellationToken cancellationToken) =>
        db.Movies.SingleOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);

    public Task<Screening?> GetScreeningAsync(int screeningId, CancellationToken cancellationToken) =>
        db.Screenings
            .Include(s => s.ScreeningSeats)
            .SingleOrDefaultAsync(s => s.ScreeningId == screeningId, cancellationToken);

    public Task<List<Movie>> GetSchedulableMoviesAsync(CancellationToken cancellationToken) => db.Movies
        .AsNoTracking()
        .Where(m => m.Status == "Upcoming" || m.Status == "Showing")
        .OrderBy(m => m.Title)
        .ToListAsync(cancellationToken);

    public Task<List<Auditorium>> GetActiveAuditoriumsAsync(CancellationToken cancellationToken) => db.Auditoriums
        .AsNoTracking()
        .Where(a => a.Status == "Active")
        .OrderBy(a => a.Name)
        .ToListAsync(cancellationToken);

    public Task<List<Seat>> GetActiveSeatsAsync(int auditoriumId, CancellationToken cancellationToken) => db.Seats
        .AsNoTracking()
        .Include(s => s.SeatType)
        .Where(s => s.AuditoriumId == auditoriumId && s.Status == "Active" && s.SeatType.IsActive)
        .OrderBy(s => s.RowLabel)
        .ThenBy(s => s.SeatNumber)
        .ToListAsync(cancellationToken);

    public Task<List<Screening>> GetScreeningsAsync(DateTime fromUtc, DateTime toUtc, int? auditoriumId, CancellationToken cancellationToken)
    {
        var query = db.Screenings.AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .Where(s => s.StartAt >= fromUtc && s.StartAt < toUtc);
        if (auditoriumId.HasValue)
            query = query.Where(s => s.AuditoriumId == auditoriumId.Value);
        return query.OrderBy(s => s.StartAt).ToListAsync(cancellationToken);
    }

    public Task<bool> AuditoriumIsActiveAsync(int auditoriumId, CancellationToken cancellationToken) =>
        db.Auditoriums.AnyAsync(a => a.AuditoriumId == auditoriumId && a.Status == "Active", cancellationToken);

    public async Task<bool> HasBookingOrTicketAsync(int screeningId, CancellationToken cancellationToken) =>
        await db.Bookings.AnyAsync(b => b.ScreeningId == screeningId, cancellationToken)
        || await db.Tickets.AnyAsync(t => t.ScreeningId == screeningId, cancellationToken);

    public Task<List<Screening>> GetScheduleConflictsAsync(int auditoriumId, DateTime startAt, DateTime endAt, int? exceptScreeningId, CancellationToken cancellationToken)
    {
        var occupiedUntil = endAt.AddMinutes(15);
        return db.Screenings
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .Where(s =>
            s.AuditoriumId == auditoriumId
            && s.Status != "Cancelled"
            && (!exceptScreeningId.HasValue || s.ScreeningId != exceptScreeningId.Value)
            && s.StartAt < occupiedUntil
            && s.EndAt.AddMinutes(s.CleanupMinutes) > startAt)
            .OrderBy(s => s.StartAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Screening>> GetScreeningsOverlappingAsync(IReadOnlyCollection<int> auditoriumIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) => db.Screenings
        .AsNoTracking()
        .Include(s => s.Movie)
        .Include(s => s.Auditorium)
        .Where(s => auditoriumIds.Contains(s.AuditoriumId)
            && s.Status != "Cancelled"
            && s.StartAt < toUtc
            && s.EndAt.AddMinutes(s.CleanupMinutes) > fromUtc)
        .OrderBy(s => s.StartAt)
        .ToListAsync(cancellationToken);

    public void Add(Screening screening) => db.Screenings.Add(screening);

    public void AddScreeningSeats(IEnumerable<ScreeningSeat> seats) => db.ScreeningSeats.AddRange(seats);

    public void RemoveScreeningSeats(IEnumerable<ScreeningSeat> seats) => db.ScreeningSeats.RemoveRange(seats);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
