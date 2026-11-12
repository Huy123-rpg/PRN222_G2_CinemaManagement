namespace PRN222_CinemaManagement.Models.Entities;

public class BookingConcession
{
    public int BookingConcessionId { get; set; }
    public int BookingId { get; set; }
    public int ConcessionItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public Booking Booking { get; set; } = null!;
    public ConcessionItem ConcessionItem { get; set; } = null!;
}
