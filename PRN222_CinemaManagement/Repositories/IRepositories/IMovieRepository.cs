using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface IMovieRepository
{
    Task<List<Movie>> GetMoviesAsync(string status, string? search, int? genreId);
    Task<List<Genre>> GetGenresAsync();
    Task<Movie?> GetMovieAsync(int movieId);
    Task<bool> TitleExistsAsync(string title, int? exceptMovieId = null);
    Task<bool> HasFutureScreeningAsync(int movieId);
    void ReplaceGenres(Movie movie, IEnumerable<int> genreIds);
    Task<int> SaveAsync(Movie movie);
    Task SaveChangesAsync();
}
