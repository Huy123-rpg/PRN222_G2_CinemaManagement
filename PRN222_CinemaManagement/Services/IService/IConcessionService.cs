using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Services.IService;

public sealed record ConcessionSaleItemRequest(int ItemId, int Quantity);
public sealed record SellConcessionRequest(int StaffUserId, List<ConcessionSaleItemRequest> Items, string PaymentMethod);

/// <summary>
/// Business logic contract for Concession sales and inventory management.
/// </summary>
public interface IConcessionService
{
    Task<List<ConcessionItem>> GetActiveItemsAsync();
    Task<List<ConcessionItem>> GetLowStockItemsAsync();
    Task<ConcessionSale?> GetSaleDetailsAsync(int saleId);
    Task<ServiceResult<ConcessionSale>> SellAsync(SellConcessionRequest request);
}
