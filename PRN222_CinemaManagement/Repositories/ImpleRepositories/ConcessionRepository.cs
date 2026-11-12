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

    public Task<List<ConcessionItem>> GetLowStockItemsAsync() =>
        db.ConcessionItems
            .AsNoTracking()
            .Where(c => c.Status == "Active" && c.StockQuantity <= c.LowStockThreshold)
            .OrderBy(c => c.StockQuantity)
            .ToListAsync();

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
