namespace PRN222_CinemaManagement.Models.Entities;

public class Ticket
{
    public int TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public int BookingId { get; set; }
    public int ShowtimeSeatId { get; set; }
    public int ShowtimeId { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Valid";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedInAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public ShowtimeSeat ShowtimeSeat { get; set; } = null!;
    public Showtime Showtime { get; set; } = null!;
}
