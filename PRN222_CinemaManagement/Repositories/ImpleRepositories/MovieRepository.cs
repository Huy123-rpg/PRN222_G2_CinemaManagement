using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class MovieRepository(CinemaDbContext db) : IMovieRepository
{
    public Task<List<Movie>> GetMoviesAsync(string status, string? search, int? genreId)
    {
        var query = db.Movies.AsNoTracking()
            .Where(m => m.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Title.Contains(search));
        if (genreId.HasValue)
            query = query.Where(m => m.MovieGenres.Any(g => g.GenreId == genreId.Value));

        return query.Include(m => m.MovieGenres)
            .ThenInclude(g => g.Genre)
            .OrderBy(m => m.Title)
            .ToListAsync();
    }

    public Task<List<Genre>> GetGenresAsync() => db.Genres
        .AsNoTracking()
        .OrderBy(g => g.GenreName)
        .ToListAsync();

    public Task<Movie?> GetMovieAsync(int movieId) => db.Movies
        .Include(m => m.MovieGenres)
        .FirstOrDefaultAsync(m => m.MovieId == movieId);

    public Task<bool> TitleExistsAsync(string title, int? exceptMovieId = null) => db.Movies
        .AnyAsync(m => m.Title.ToLower() == title.ToLower()
            && (!exceptMovieId.HasValue || m.MovieId != exceptMovieId.Value));

    public Task<bool> HasFutureScreeningAsync(int movieId) => db.Screenings
        .AnyAsync(s => s.MovieId == movieId
            && s.StartAt > DateTime.UtcNow
            && s.Status != "Cancelled");

    public void ReplaceGenres(Movie movie, IEnumerable<int> genreIds)
    {
        db.MovieGenres.RemoveRange(movie.MovieGenres);
        movie.MovieGenres.Clear();
        foreach (var genreId in genreIds)
            db.MovieGenres.Add(new MovieGenre { MovieId = movie.MovieId, GenreId = genreId });
    }

    public async Task<int> SaveAsync(Movie movie)
    {
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        return movie.MovieId;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
