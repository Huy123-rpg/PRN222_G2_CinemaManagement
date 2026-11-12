namespace PRN222_CinemaManagement.Models.Entities;

public class Payment
{
    public int PaymentId { get; set; }
    public int BookingId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? TransactionRef { get; set; }
    public decimal Amount { get; set; }
    public decimal RefundAmount { get; set; } = 0m;
    public string PaymentMethod { get; set; } = "Cash";
    public string PaymentStatus { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Booking Booking { get; set; } = null!;
}
