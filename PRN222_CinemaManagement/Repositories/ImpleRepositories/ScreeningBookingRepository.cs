using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class ScreeningBookingRepository(CinemaDbContext context)
    : GenericRepository<Screening>(context), IScreeningBookingRepository
{
    public async Task<IReadOnlyList<Screening>> GetUpcomingByMovieIdAsync(int movieId, DateTime utcNow, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(s => s.Auditorium)
            .Where(s => s.MovieId == movieId
                && s.Status == BookingConstants.ScreeningStatuses.Scheduled
                && s.StartAt > utcNow
                && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active)
            .OrderBy(s => s.StartAt)
            .ToListAsync(ct);
    }

    public Task<Screening?> GetOpenScreeningByIdAsync(int screeningId, DateTime utcNow, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .FirstOrDefaultAsync(s => s.ScreeningId == screeningId
                && s.Status == BookingConstants.ScreeningStatuses.Scheduled
                && s.StartAt > utcNow
                && s.Movie.Status == BookingConstants.MovieStatuses.Showing
                && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active, ct);
    }

    public Task<bool> ExistsForShowingMovieAsync(int screeningId, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .AnyAsync(s => s.ScreeningId == screeningId
                && s.Movie.Status == BookingConstants.MovieStatuses.Showing, ct);
    }
}

