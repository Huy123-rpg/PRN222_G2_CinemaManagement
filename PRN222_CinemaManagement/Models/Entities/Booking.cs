namespace PRN222_CinemaManagement.Models.Entities;

public class Booking
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? StaffId { get; set; }
    public int ShowtimeId { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public decimal TotalAmount { get; set; } = 0m;
    public string BookingChannel { get; set; } = "Online";
    public string Status { get; set; } = "Pending";
    public byte[] RowVersion { get; set; } = [];

    public Customer? Customer { get; set; }
    public Staff? Staff { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public ICollection<ShowtimeSeat> HeldShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<BookingConcession> BookingConcessions { get; set; } = new List<BookingConcession>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
