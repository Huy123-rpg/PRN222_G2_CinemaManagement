namespace PRN222_CinemaManagement.Models.Entities;

public class ConcessionSale
{
    public int ConcessionSaleId { get; set; }
    public string SaleCode { get; set; } = string.Empty;
    public int? StaffId { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; } = 0m;
    public string PaymentMethod { get; set; } = "Cash";
    public string Status { get; set; } = "Completed";
    public byte[] RowVersion { get; set; } = [];

    public Staff? Staff { get; set; }
    public ICollection<ConcessionSaleItem> ConcessionSaleItems { get; set; } = new List<ConcessionSaleItem>();
}
