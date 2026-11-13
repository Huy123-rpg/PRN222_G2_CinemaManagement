namespace PRN222_CinemaManagement.Models.Entities;

/// <summary>A scheduled showing of one movie in one auditorium.</summary>
public class Screening
{
    public int ScreeningId { get; set; }
    public int MovieId { get; set; }
    public int AuditoriumId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int CleanupMinutes { get; set; } = 15;
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = "Scheduled";
    public byte[] RowVersion { get; set; } = [];

    public Movie Movie { get; set; } = null!;
    public Auditorium Auditorium { get; set; } = null!;
    public ICollection<ScreeningSeat> ScreeningSeats { get; set; } = new List<ScreeningSeat>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
