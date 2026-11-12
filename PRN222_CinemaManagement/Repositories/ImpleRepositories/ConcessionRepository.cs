using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

/// <summary>
/// EF Core implementation of <see cref="IConcessionRepository"/>.
/// </summary>
public class ConcessionRepository(CinemaDbContext db) : IConcessionRepository
{
    public Task<List<ConcessionItem>> GetActiveItemsAsync() =>
        db.ConcessionItems
            .AsNoTracking()
            .Where(c => c.Status == "Active")
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<(List<ConcessionItem> Items, int TotalCount)> GetActiveItemsPagedAsync(int page, int pageSize)
    {
        var query = db.ConcessionItems
            .AsNoTracking()
            .Where(c => c.Status == "Active");

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
        db.ConcessionItems
            .AsNoTracking()
            .Where(c => c.Status == "Active" && c.StockQuantity <= c.LowStockThreshold)
            .OrderBy(c => c.StockQuantity)
            .ToListAsync();

    public async Task<(List<ConcessionItem> Items, int TotalCount, int OutOfStockCount, int LowStockCount)> GetLowStockItemsPagedAsync(int page, int pageSize)
    {
        var allLowStock = await db.ConcessionItems
            .AsNoTracking()
            .Where(c => c.Status == "Active" && c.StockQuantity <= c.LowStockThreshold)
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
        db.ConcessionSales
            .AsNoTracking()
            .Include(s => s.Staff).ThenInclude(st => st!.User)
            .Include(s => s.ConcessionSaleItems).ThenInclude(si => si.ConcessionItem)
            .FirstOrDefaultAsync(s => s.ConcessionSaleId == saleId);

    public Task<Staff?> GetStaffByUserIdAsync(int userId) =>
        db.Staffs.FirstOrDefaultAsync(s => s.UserId == userId);

    public Task<List<ConcessionItem>> GetItemsByIdsAsync(IEnumerable<int> ids) =>
        db.ConcessionItems
            .Where(c => ids.Contains(c.ConcessionItemId))
            .ToListAsync();

    public void AddSale(ConcessionSale sale) => db.ConcessionSales.Add(sale);

    public Task SaveChangesAsync() => db.SaveChangesAsync();

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
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
