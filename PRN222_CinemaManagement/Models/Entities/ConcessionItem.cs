namespace PRN222_CinemaManagement.Models.Entities;

public class ConcessionItem
{
    public int ConcessionItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; } = 0;
    public int LowStockThreshold { get; set; } = 10;
    public string Status { get; set; } = "Active";
    public byte[] RowVersion { get; set; } = [];

    public ICollection<BookingConcession> BookingConcessions { get; set; } = new List<BookingConcession>();
}
