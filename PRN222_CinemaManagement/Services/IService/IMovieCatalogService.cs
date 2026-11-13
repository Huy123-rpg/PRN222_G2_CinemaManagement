using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public interface IMovieCatalogService
{
    Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default);
    Task<MovieCatalogViewModel> GetMovieCatalogAsync(MovieFilterViewModel? filter, CancellationToken ct = default);
    Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default);
    Task<SeatMapViewModel?> GetSeatMapAsync(int screeningId, int? userId, CancellationToken ct = default);
    Task<bool> IsScreeningAvailableForUpdatesAsync(int screeningId, CancellationToken ct = default);
}

