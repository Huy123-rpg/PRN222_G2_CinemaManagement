namespace PRN222_CinemaManagement.Models.Entities;

public class ConcessionSaleItem
{
    public int ConcessionSaleItemId { get; set; }
    public int ConcessionSaleId { get; set; }
    public int ConcessionItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public ConcessionSale ConcessionSale { get; set; } = null!;
    public ConcessionItem ConcessionItem { get; set; } = null!;
}
