namespace PRN222_CinemaManagement.Models.Entities;

public class Showtime
{
    public int ShowtimeId { get; set; }
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
    public ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
