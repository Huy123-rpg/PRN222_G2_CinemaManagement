using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.Interfaces;

namespace PRN222_CinemaManagement.Repositories.Implementations;

public class ShowtimeRepository(CinemaDbContext context)
    : GenericRepository<Showtime>(context), IShowtimeRepository
{
    public async Task<IReadOnlyList<Showtime>> GetUpcomingByMovieIdAsync(int movieId, DateTime utcNow, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(s => s.Auditorium)
            .Where(s => s.MovieId == movieId
                && s.Status == BookingConstants.ShowtimeStatuses.Scheduled
                && s.StartAt > utcNow
                && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active)
            .OrderBy(s => s.StartAt)
            .ToListAsync(ct);
    }

    public Task<Showtime?> GetOpenShowtimeByIdAsync(int showtimeId, DateTime utcNow, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId
                && s.Status == BookingConstants.ShowtimeStatuses.Scheduled
                && s.StartAt > utcNow
                && s.Movie.Status == BookingConstants.MovieStatuses.Showing
                && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active, ct);
    }

    public Task<bool> ExistsForShowingMovieAsync(int showtimeId, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .AnyAsync(s => s.ShowtimeId == showtimeId
                && s.Movie.Status == BookingConstants.MovieStatuses.Showing, ct);
    }
}

