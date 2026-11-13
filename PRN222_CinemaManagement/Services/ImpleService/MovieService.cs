using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.ImpleService;

public class MovieService(IMovieRepository movies) : IMovieService
{
    public async Task<MovieListViewModel> GetMovieListAsync(string? tab, string? search, int? genreId)
    {
        tab = tab is "upcoming" or "ended" ? tab : "showing";
        search = search?.Trim();

        var entities = await movies.GetMoviesAsync(
            tab switch { "upcoming" => "Upcoming", "ended" => "Ended", _ => "Showing" }, search, genreId);
        var genres = await movies.GetGenresAsync();

        return new MovieListViewModel
        {
            Tab = tab,
            Search = search,
            GenreId = genreId,
            Genres = genres.Select(g => new GenreItemViewModel
            {
                Id = g.GenreId,
                Name = g.GenreName
            }).ToList(),
            Movies = entities.Select(m => new MovieItemViewModel
            {
                Id = m.MovieId,
                Title = m.Title,
                PosterUrl = m.PosterUrl,
                AgeRating = m.AgeRating,
                DurationMinutes = m.DurationMinutes,
                ReleaseDate = m.ReleaseDate,
                Status = m.Status,
                Genres = m.MovieGenres.Select(g => g.Genre.GenreName).ToList()
            }).ToList()
        };
    }

    public async Task<CreateMovieViewModel> GetCreateMovieModelAsync() => new()
    {
        Genres = await GetGenresAsync()
    };

    public async Task<EditMovieViewModel?> GetEditMovieModelAsync(int movieId)
    {
        var movie = await movies.GetMovieAsync(movieId);
        if (movie == null) return null;

        return new EditMovieViewModel
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            Description = movie.Description,
            AgeRating = movie.AgeRating,
            ReleaseDate = movie.ReleaseDate,
            PosterUrl = movie.PosterUrl ?? "",
            TrailerUrl = movie.TrailerUrl,
            GenreIds = movie.MovieGenres.Select(g => g.GenreId).ToList(),
            Genres = await GetGenresAsync()
        };
    }

    public async Task<ServiceResult<int>> CreateMovieAsync(CreateMovieViewModel model)
    {
        var title = model.Title.Trim();
        if (await movies.TitleExistsAsync(title))
            return ServiceResult<int>.Fail("Title", "Tên phim này đã tồn tại.");
        if (!await HasValidGenresAsync(model.GenreIds))
            return ServiceResult<int>.Fail("GenreIds", "Danh sách thể loại không hợp lệ.");

        var movie = new Movie
        {
            Title = title,
            Description = model.Description?.Trim(),
            DurationMinutes = model.DurationMinutes,
            AgeRating = model.AgeRating,
            ReleaseDate = model.ReleaseDate,
            PosterUrl = model.PosterUrl.Trim(),
            TrailerUrl = NullIfBlank(model.TrailerUrl),
            Status = "Upcoming",
            MovieGenres = model.GenreIds.Distinct().Select(id => new MovieGenre { GenreId = id }).ToList()
        };

        var movieId = await movies.SaveAsync(movie);
        return ServiceResult<int>.Success(movieId, "Đã thêm phim.");
    }

    public async Task<ServiceResult> UpdateMovieAsync(int movieId, EditMovieViewModel model)
    {
        var movie = await movies.GetMovieAsync(movieId);
        if (movie == null) return ServiceResult.NotFound();

        var title = model.Title.Trim();
        if (await movies.TitleExistsAsync(title, movieId))
            return ServiceResult.Fail("Title", "Tên phim này đã tồn tại.");
        if (!await HasValidGenresAsync(model.GenreIds))
            return ServiceResult.Fail("GenreIds", "Danh sách thể loại không hợp lệ.");

        movie.Title = title;
        movie.Description = model.Description?.Trim();
        movie.AgeRating = model.AgeRating;
        movie.ReleaseDate = model.ReleaseDate;
        movie.PosterUrl = model.PosterUrl.Trim();
        movie.TrailerUrl = NullIfBlank(model.TrailerUrl);
        movies.ReplaceGenres(movie, model.GenreIds.Distinct());

        await movies.SaveChangesAsync();
        return ServiceResult.Success("Đã cập nhật phim.");
    }

    public async Task<ServiceResult> ChangeStatusAsync(int movieId, string? newStatus, string? role)
    {
        var movie = await movies.GetMovieAsync(movieId);
        if (movie == null) return ServiceResult.NotFound();

        var canMoveForward = (movie.Status, newStatus) switch
        {
            ("Upcoming", "Showing") => true,
            ("Showing", "Ended") => true,
            _ => false
        };
        var canMoveBack = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            && ((movie.Status, newStatus) is ("Showing", "Upcoming") or ("Ended", "Showing"));

        if (!canMoveForward && !canMoveBack)
            return ServiceResult.Fail("Status", "Không thể chuyển phim sang trạng thái đã chọn.");

        if (newStatus == "Ended" && await movies.HasFutureScreeningAsync(movieId))
            return ServiceResult.Fail("Status", "Không thể kết thúc phim khi vẫn còn suất chiếu trong tương lai chưa bị hủy.");

        movie.Status = newStatus!;
        await movies.SaveChangesAsync();
        return ServiceResult.Success($"Đã chuyển phim sang trạng thái {newStatus}.");
    }

    public async Task<List<GenreItemViewModel>> GetGenresAsync() =>
        (await movies.GetGenresAsync()).Select(g => new GenreItemViewModel
        {
            Id = g.GenreId,
            Name = g.GenreName
        }).ToList();

    private async Task<bool> HasValidGenresAsync(List<int> ids)
    {
        var validIds = (await movies.GetGenresAsync()).Select(g => g.GenreId).ToHashSet();
        return ids.Distinct().Count() == ids.Count && ids.All(validIds.Contains);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
