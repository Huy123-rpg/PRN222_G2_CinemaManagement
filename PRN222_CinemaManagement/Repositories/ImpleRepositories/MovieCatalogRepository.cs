using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.DTOs;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class MovieCatalogRepository(CinemaDbContext context)
    : GenericRepository<Movie>(context), IMovieCatalogRepository
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Dictionary<string, int> AgeRatingOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["P"] = 1,
        ["K"] = 2,
        ["T13"] = 3,
        ["C13"] = 3,
        ["T16"] = 4,
        ["C16"] = 4,
        ["T18"] = 5,
        ["C18"] = 5
    };

    public async Task<IReadOnlyList<Movie>> GetShowingMoviesAsync(CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(m => m.MovieGenres)
            .ThenInclude(mg => mg.Genre)
            .Where(m => m.Status == BookingConstants.MovieStatuses.Showing)
            .OrderBy(m => m.Title)
            .ToListAsync(ct);
    }

    public Task<Movie?> GetShowingMovieByIdAsync(int movieId, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .Include(m => m.MovieGenres)
            .ThenInclude(mg => mg.Genre)
            .FirstOrDefaultAsync(m => m.MovieId == movieId
                && m.Status == BookingConstants.MovieStatuses.Showing, ct);
    }

    public async Task<IReadOnlyList<MovieCatalogItemDto>> SearchShowingMoviesAsync(
        MovieSearchCriteria criteria,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(m => m.Status == BookingConstants.MovieStatuses.Showing);

        if (criteria.GenreId.HasValue)
        {
            var genreId = criteria.GenreId.Value;
            query = query.Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId));
        }

        if (!string.IsNullOrWhiteSpace(criteria.AgeRating))
        {
            var ageRating = criteria.AgeRating.Trim();
            query = query.Where(m => m.AgeRating == ageRating);
        }

        if (criteria.ShowDate.HasValue)
        {
            // Ngày chiếu trên giao diện tính theo giờ Việt Nam (UTC+7).
            var vnDayStartUtc = criteria.ShowDate.Value
                .ToDateTime(TimeOnly.MinValue)
                .AddHours(-BookingConstants.VietnamUtcOffsetHours);
            var vnDayEndUtc = vnDayStartUtc.AddDays(1);

            query = query.Where(m => m.Screenings.Any(s =>
                s.Status == BookingConstants.ScreeningStatuses.Scheduled
                && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active
                && s.StartAt > utcNow
                && s.StartAt >= vnDayStartUtc
                && s.StartAt < vnDayEndUtc));
        }
        else if (criteria.OnlyWithScreenings)
        {
            query = query.Where(m => m.Screenings.Any(s =>
                s.Status == BookingConstants.ScreeningStatuses.Scheduled
                && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active
                && s.StartAt > utcNow));
        }

        var projected = await query
            .Select(m => new MovieCatalogItemDto
            {
                MovieId = m.MovieId,
                Title = m.Title,
                Description = m.Description,
                DurationMinutes = m.DurationMinutes,
                AgeRating = m.AgeRating,
                ReleaseDate = m.ReleaseDate,
                PosterUrl = m.PosterUrl,
                Genres = m.MovieGenres
                    .OrderBy(mg => mg.Genre.GenreName)
                    .Select(mg => mg.Genre.GenreName)
                    .ToList(),
                UpcomingScreeningCount = m.Screenings.Count(s =>
                    s.Status == BookingConstants.ScreeningStatuses.Scheduled
                    && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active
                    && s.StartAt > utcNow)
            })
            .ToListAsync(ct);

        IEnumerable<MovieCatalogItemDto> filtered = projected;
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var tokens = TokenizeNormalized(criteria.Search);
            if (tokens.Length > 0)
            {
                filtered = filtered.Where(item => MatchesTokens(item, tokens));
            }
        }

        return SortMovies(filtered, criteria.SortBy).ToList();
    }

    public async Task<MovieFilterOptionsDto> GetShowingFilterOptionsAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var totalShowing = await DbSet
            .AsNoTracking()
            .CountAsync(m => m.Status == BookingConstants.MovieStatuses.Showing, ct);

        var genres = await Context.Genres
            .AsNoTracking()
            .Select(g => new GenreFilterOptionDto
            {
                GenreId = g.GenreId,
                GenreName = g.GenreName,
                MovieCount = g.MovieGenres.Count(mg => mg.Movie.Status == BookingConstants.MovieStatuses.Showing)
            })
            .Where(g => g.MovieCount > 0)
            .OrderBy(g => g.GenreName)
            .ToListAsync(ct);

        var rawRatings = await DbSet
            .AsNoTracking()
            .Where(m => m.Status == BookingConstants.MovieStatuses.Showing && !string.IsNullOrEmpty(m.AgeRating))
            .Select(m => m.AgeRating)
            .Distinct()
            .ToListAsync(ct);

        var ageRatings = rawRatings
            .OrderBy(r => AgeRatingOrder.TryGetValue(r, out var rank) ? rank : int.MaxValue)
            .ThenBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var upcomingStarts = await Context.Screenings
            .AsNoTracking()
            .Where(s => s.Status == BookingConstants.ScreeningStatuses.Scheduled
                && s.StartAt > utcNow
                && s.Movie.Status == BookingConstants.MovieStatuses.Showing
                && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active)
            .Select(s => s.StartAt)
            .ToListAsync(ct);

        var showDates = upcomingStarts
            .Select(utc => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc)
                .AddHours(BookingConstants.VietnamUtcOffsetHours)))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return new MovieFilterOptionsDto
        {
            TotalShowingCount = totalShowing,
            Genres = genres,
            AgeRatings = ageRatings,
            ShowDates = showDates
        };
    }

    private static IEnumerable<MovieCatalogItemDto> SortMovies(IEnumerable<MovieCatalogItemDto> items, string sortBy)
    {
        var titleComparer = StringComparer.Create(VietnameseCulture, ignoreCase: true);

        return sortBy switch
        {
            BookingConstants.MovieSortOptions.TitleDesc =>
                items.OrderByDescending(m => m.Title, titleComparer),
            BookingConstants.MovieSortOptions.Newest =>
                items.OrderByDescending(m => m.ReleaseDate ?? DateOnly.MinValue)
                     .ThenBy(m => m.Title, titleComparer),
            BookingConstants.MovieSortOptions.DurationAsc =>
                items.OrderBy(m => m.DurationMinutes)
                     .ThenBy(m => m.Title, titleComparer),
            BookingConstants.MovieSortOptions.DurationDesc =>
                items.OrderByDescending(m => m.DurationMinutes)
                     .ThenBy(m => m.Title, titleComparer),
            BookingConstants.MovieSortOptions.ScreeningsDesc =>
                items.OrderByDescending(m => m.UpcomingScreeningCount)
                     .ThenBy(m => m.Title, titleComparer),
            _ =>
                items.OrderBy(m => m.Title, titleComparer)
        };
    }

    private static bool MatchesTokens(MovieCatalogItemDto item, string[] tokens)
    {
        var searchable = NormalizeSearchText(
            string.Join(' ', item.Title, item.Description ?? string.Empty, string.Join(' ', item.Genres)));

        return tokens.All(token => searchable.Contains(token, StringComparison.Ordinal));
    }

    private static string[] TokenizeNormalized(string text)
        => NormalizeSearchText(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Chuẩn hóa tiếng Việt (bỏ dấu & đ -> d) để tìm được cả khi nhập có dấu lẫn không dấu.
    private static string NormalizeSearchText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var formD = input.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(ch switch
            {
                'đ' => 'd',
                'Đ' => 'd',
                _ => char.ToLowerInvariant(ch)
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
