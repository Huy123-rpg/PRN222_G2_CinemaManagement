namespace PRN222_CinemaManagement.ViewModels;

public class MovieListViewModel
{
    public string Tab { get; set; } = "showing";
    public string? Search { get; set; }
    public int? GenreId { get; set; }
    public bool CanManage { get; set; }
    public bool CanReverseStatus { get; set; }
    public List<GenreItemViewModel> Genres { get; set; } = [];
    public List<MovieItemViewModel> Movies { get; set; } = [];
}

public class GenreItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class MovieItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string AgeRating { get; set; } = "P";
    public int DurationMinutes { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string Status { get; set; } = "Upcoming";
    public List<string> Genres { get; set; } = [];
}
