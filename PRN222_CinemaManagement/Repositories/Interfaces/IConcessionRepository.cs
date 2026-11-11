using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.Interfaces;

public interface IConcessionRepository : IGenericRepository<ConcessionItem>
{
    Task<IReadOnlyList<ConcessionItem>> GetActiveItemsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ConcessionItem>> GetActiveItemsByIdsAsync(IEnumerable<int> itemIds, CancellationToken ct = default);
    Task<int> TryDeductStockAsync(int concessionItemId, int quantity, CancellationToken ct = default);
}

