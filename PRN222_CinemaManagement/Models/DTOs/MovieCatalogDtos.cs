using PRN222_CinemaManagement.Constants;

namespace PRN222_CinemaManagement.Models.DTOs;

public class MovieSearchCriteria
{
    public string? Search { get; set; }
    public int? GenreId { get; set; }
    public string? AgeRating { get; set; }
    public DateOnly? ShowDate { get; set; }
    public string SortBy { get; set; } = BookingConstants.MovieSortOptions.TitleAsc;
    public bool OnlyWithScreenings { get; set; }
}

public class MovieCatalogItemDto
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string AgeRating { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public string? PosterUrl { get; set; }
    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();
    public int UpcomingScreeningCount { get; set; }
}

public class GenreFilterOptionDto
{
    public int GenreId { get; set; }
    public string GenreName { get; set; } = string.Empty;
    public int MovieCount { get; set; }
}

public class MovieFilterOptionsDto
{
    public int TotalShowingCount { get; set; }
    public IReadOnlyList<GenreFilterOptionDto> Genres { get; set; } = Array.Empty<GenreFilterOptionDto>();
    public IReadOnlyList<string> AgeRatings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DateOnly> ShowDates { get; set; } = Array.Empty<DateOnly>();
}

