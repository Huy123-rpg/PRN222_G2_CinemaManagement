using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.Interfaces;

public interface IMovieCatalogService
{
    Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default);
    Task<MovieCatalogViewModel> GetMovieCatalogAsync(MovieFilterViewModel? filter, CancellationToken ct = default);
    Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default);
    Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default);
    Task<bool> IsShowtimeAvailableForUpdatesAsync(int showtimeId, CancellationToken ct = default);
}

