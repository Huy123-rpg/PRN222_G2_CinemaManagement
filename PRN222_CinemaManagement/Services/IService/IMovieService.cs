using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public interface IMovieService
{
    Task<MovieListViewModel> GetMovieListAsync(string? tab, string? search, int? genreId);
    Task<List<GenreItemViewModel>> GetGenresAsync();
    Task<CreateMovieViewModel> GetCreateMovieModelAsync();
    Task<EditMovieViewModel?> GetEditMovieModelAsync(int movieId);
    Task<ServiceResult<int>> CreateMovieAsync(CreateMovieViewModel model);
    Task<ServiceResult> UpdateMovieAsync(int movieId, EditMovieViewModel model);
    Task<ServiceResult> ChangeStatusAsync(int movieId, string? newStatus, string? role);
}
