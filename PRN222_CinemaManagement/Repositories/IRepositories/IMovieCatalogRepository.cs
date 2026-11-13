using PRN222_CinemaManagement.Models.DTOs;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface IMovieCatalogRepository : IGenericRepository<Movie>
{
    Task<IReadOnlyList<Movie>> GetShowingMoviesAsync(CancellationToken ct = default);
    Task<Movie?> GetShowingMovieByIdAsync(int movieId, CancellationToken ct = default);
    Task<IReadOnlyList<MovieCatalogItemDto>> SearchShowingMoviesAsync(
        MovieSearchCriteria criteria,
        DateTime utcNow,
        CancellationToken ct = default);
    Task<MovieFilterOptionsDto> GetShowingFilterOptionsAsync(DateTime utcNow, CancellationToken ct = default);
}
