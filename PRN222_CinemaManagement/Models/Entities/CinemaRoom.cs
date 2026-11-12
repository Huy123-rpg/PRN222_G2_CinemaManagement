using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.Models.Entities;

public sealed class CinemaRoom
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}
