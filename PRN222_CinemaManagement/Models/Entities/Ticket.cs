namespace PRN222_CinemaManagement.Models.Entities;

public class Ticket
{
    public int TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public int BookingId { get; set; }
    public int ScreeningSeatId { get; set; }
    public int ScreeningId { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Valid";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedInAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public ScreeningSeat ScreeningSeat { get; set; } = null!;
    public Screening Screening { get; set; } = null!;
}
