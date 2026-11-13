namespace PRN222_CinemaManagement.Models.Entities;

public class ScreeningSeat
{
    public int ScreeningSeatId { get; set; }
    public int ScreeningId { get; set; }
    public int SeatId { get; set; }
    public int AuditoriumId { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Available";
    public int? HeldByBookingId { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Screening Screening { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
    public Auditorium Auditorium { get; set; } = null!;
    public Booking? HeldByBooking { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
