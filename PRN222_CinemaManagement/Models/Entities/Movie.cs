using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.Models.Entities;

public sealed class Movie
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1, 600)]
    public int DurationMinutes { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public MovieStatus Status { get; set; } = MovieStatus.ComingSoon;

    [StringLength(500), Url]
    public string? PosterUrl { get; set; }

    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}
