namespace PRN222_CinemaManagement.Models.Entities;

public class Movie
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string AgeRating { get; set; } = "P";
    public DateOnly? ReleaseDate { get; set; }
    public string? PosterUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string Status { get; set; } = "Upcoming";

    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}
