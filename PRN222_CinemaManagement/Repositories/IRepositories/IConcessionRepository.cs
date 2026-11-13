using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

/// <summary>
/// Data access contract for Concession management (Online Booking & In-store Counter UC-15, UC-16, UC-17).
/// </summary>
public interface IConcessionRepository : IGenericRepository<ConcessionItem>
{
    // Online Booking methods
    Task<List<ConcessionItem>> GetActiveItemsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ConcessionItem>> GetActiveItemsByIdsAsync(IEnumerable<int> itemIds, CancellationToken ct = default);
    Task<int> TryDeductStockAsync(int concessionItemId, int quantity, CancellationToken ct = default);

    // In-store Counter & Inventory methods
    Task<(List<ConcessionItem> Items, int TotalCount)> GetActiveItemsPagedAsync(int page, int pageSize);
    Task<List<ConcessionItem>> GetLowStockItemsAsync();
    Task<(List<ConcessionItem> Items, int TotalCount, int OutOfStockCount, int LowStockCount)> GetLowStockItemsPagedAsync(int page, int pageSize);
    Task<ConcessionSale?> GetSaleByIdAsync(int saleId);
    Task<Staff?> GetStaffByUserIdAsync(int userId);
    Task<List<ConcessionItem>> GetItemsByIdsAsync(IEnumerable<int> ids);
    void AddSale(ConcessionSale sale);
    Task SaveChangesAsync();
    Task ExecuteInTransactionAsync(Func<Task> operation);
}
