using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.Interfaces;

namespace PRN222_CinemaManagement.Repositories.Implementations;

public class ConcessionRepository(CinemaDbContext context)
    : GenericRepository<ConcessionItem>(context), IConcessionRepository
{
    public async Task<IReadOnlyList<ConcessionItem>> GetActiveItemsAsync(CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(c => c.Status == BookingConstants.ConcessionStatuses.Active)
            .OrderByDescending(c => c.StockQuantity > 0)
            .ThenBy(c => c.ConcessionItemId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConcessionItem>> GetActiveItemsByIdsAsync(IEnumerable<int> itemIds, CancellationToken ct = default)
    {
        var ids = itemIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<ConcessionItem>();

        return await DbSet
            .AsNoTracking()
            .Where(c => ids.Contains(c.ConcessionItemId)
                && c.Status == BookingConstants.ConcessionStatuses.Active)
            .OrderBy(c => c.ConcessionItemId)
            .ToListAsync(ct);
    }

    public Task<int> TryDeductStockAsync(int concessionItemId, int quantity, CancellationToken ct = default)
    {
        return DbSet
            .Where(c => c.ConcessionItemId == concessionItemId
                && c.Status == BookingConstants.ConcessionStatuses.Active
                && c.StockQuantity >= quantity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.StockQuantity, c => c.StockQuantity - quantity), ct);
    }
}

