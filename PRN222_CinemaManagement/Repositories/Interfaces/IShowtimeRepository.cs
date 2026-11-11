using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.Interfaces;

public interface IShowtimeRepository : IGenericRepository<Showtime>
{
    Task<IReadOnlyList<Showtime>> GetUpcomingByMovieIdAsync(int movieId, DateTime utcNow, CancellationToken ct = default);
    Task<Showtime?> GetOpenShowtimeByIdAsync(int showtimeId, DateTime utcNow, CancellationToken ct = default);
    Task<bool> ExistsForShowingMovieAsync(int showtimeId, CancellationToken ct = default);
}

