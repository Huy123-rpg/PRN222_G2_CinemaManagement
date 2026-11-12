namespace PRN222_CinemaManagement.Models.Entities;

public class Auditorium
{
    public int AuditoriumId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public string Status { get; set; } = "Active";

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}
