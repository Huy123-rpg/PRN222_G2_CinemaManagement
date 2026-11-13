using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface IScreeningBookingRepository : IGenericRepository<Screening>
{
    Task<IReadOnlyList<Screening>> GetUpcomingByMovieIdAsync(int movieId, DateTime utcNow, CancellationToken ct = default);
    Task<Screening?> GetOpenScreeningByIdAsync(int screeningId, DateTime utcNow, CancellationToken ct = default);
    Task<bool> ExistsForShowingMovieAsync(int screeningId, CancellationToken ct = default);
}

