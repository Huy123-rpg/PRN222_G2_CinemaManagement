using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.Models.Entities;

public sealed class Screening
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public int CinemaRoomId { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public Movie Movie { get; set; } = null!;

    public CinemaRoom CinemaRoom { get; set; } = null!;
}
