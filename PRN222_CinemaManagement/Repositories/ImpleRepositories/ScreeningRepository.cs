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
        db.Screenings.SingleOrDefaultAsync(s => s.ScreeningId == screeningId, cancellationToken);

    public Task<bool> AuditoriumIsActiveAsync(int auditoriumId, CancellationToken cancellationToken) =>
        db.Auditoriums.AnyAsync(a => a.AuditoriumId == auditoriumId && a.Status == "Active", cancellationToken);

    public async Task<bool> HasBookingOrTicketAsync(int screeningId, CancellationToken cancellationToken) =>
        await db.Bookings.AnyAsync(b => b.ScreeningId == screeningId, cancellationToken)
        || await db.Tickets.AnyAsync(t => t.ScreeningId == screeningId, cancellationToken);

    public Task<bool> HasScheduleConflictAsync(int auditoriumId, DateTime startAt, DateTime endAt, int? exceptScreeningId, CancellationToken cancellationToken)
    {
        var occupiedUntil = endAt.AddMinutes(15);
        return db.Screenings.AnyAsync(s =>
            s.AuditoriumId == auditoriumId
            && s.Status != "Cancelled"
            && (!exceptScreeningId.HasValue || s.ScreeningId != exceptScreeningId.Value)
            && s.StartAt < occupiedUntil
            && s.EndAt.AddMinutes(s.CleanupMinutes) > startAt,
            cancellationToken);
    }

    public void Add(Screening screening) => db.Screenings.Add(screening);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
