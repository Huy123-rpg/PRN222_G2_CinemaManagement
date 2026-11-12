using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

/// <summary>
/// Data access contract for Concession management (UC-15, UC-16, UC-17).
/// </summary>
public interface IConcessionRepository
{
    Task<List<ConcessionItem>> GetActiveItemsAsync();
    Task<List<ConcessionItem>> GetLowStockItemsAsync();
    Task<ConcessionSale?> GetSaleByIdAsync(int saleId);
    Task<Staff?> GetStaffByUserIdAsync(int userId);
    Task<List<ConcessionItem>> GetItemsByIdsAsync(IEnumerable<int> ids);
    void AddSale(ConcessionSale sale);
    Task SaveChangesAsync();
    Task ExecuteInTransactionAsync(Func<Task> operation);
}
