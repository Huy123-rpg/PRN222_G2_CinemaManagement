using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

/// <summary>
/// EF Core implementation of <see cref="IConcessionRepository"/>.
/// Supports both Online Booking and Counter Concession Sales.
/// </summary>
public class ConcessionRepository(CinemaDbContext context)
    : GenericRepository<ConcessionItem>(context), IConcessionRepository
{
    // ===== Online Booking Implementation =====

    public async Task<List<ConcessionItem>> GetActiveItemsAsync(CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(c => c.Status == BookingConstants.ConcessionStatuses.Active)
            .OrderByDescending(c => c.StockQuantity > 0)
            .ThenBy(c => c.Name)
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

    // ===== In-store Counter & Inventory Implementation =====

    public async Task<(List<ConcessionItem> Items, int TotalCount)> GetActiveItemsPagedAsync(int page, int pageSize)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(c => c.Status == BookingConstants.ConcessionStatuses.Active);

        int totalCount = await query.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var items = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.ConcessionItemId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<List<ConcessionItem>> GetLowStockItemsAsync() =>
        DbSet
            .AsNoTracking()
            .Where(c => c.Status == BookingConstants.ConcessionStatuses.Active && c.StockQuantity <= c.LowStockThreshold)
            .OrderBy(c => c.StockQuantity)
            .ToListAsync();

    public async Task<(List<ConcessionItem> Items, int TotalCount, int OutOfStockCount, int LowStockCount)> GetLowStockItemsPagedAsync(int page, int pageSize)
    {
        var allLowStock = await DbSet
            .AsNoTracking()
            .Where(c => c.Status == BookingConstants.ConcessionStatuses.Active && c.StockQuantity <= c.LowStockThreshold)
            .ToListAsync();

        int totalCount = allLowStock.Count;
        int outOfStock = allLowStock.Count(c => c.StockQuantity <= 0);
        int lowStock = allLowStock.Count(c => c.StockQuantity > 0 && c.StockQuantity <= c.LowStockThreshold);

        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var pagedItems = allLowStock
            .OrderBy(c => c.StockQuantity)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (pagedItems, totalCount, outOfStock, lowStock);
    }

    public Task<ConcessionSale?> GetSaleByIdAsync(int saleId) =>
        Context.ConcessionSales
            .AsNoTracking()
            .Include(s => s.Staff).ThenInclude(st => st!.User)
            .Include(s => s.ConcessionSaleItems).ThenInclude(si => si.ConcessionItem)
            .FirstOrDefaultAsync(s => s.ConcessionSaleId == saleId);

    public Task<Staff?> GetStaffByUserIdAsync(int userId) =>
        Context.Staffs.FirstOrDefaultAsync(s => s.UserId == userId);

    public Task<List<ConcessionItem>> GetItemsByIdsAsync(IEnumerable<int> ids) =>
        DbSet
            .Where(c => ids.Contains(c.ConcessionItemId))
            .ToListAsync();

    public void AddSale(ConcessionSale sale) => Context.ConcessionSales.Add(sale);

    public Task SaveChangesAsync() => Context.SaveChangesAsync();

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        try
        {
            await operation();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
