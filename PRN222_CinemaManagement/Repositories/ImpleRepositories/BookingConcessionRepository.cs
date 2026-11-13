using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class BookingConcessionRepository(CinemaDbContext context)
    : GenericRepository<BookingConcession>(context), IBookingConcessionRepository
{
    public async Task<IReadOnlyList<BookingConcession>> GetByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(bc => bc.BookingId == bookingId)
            .OrderBy(bc => bc.ConcessionItemId)
            .ToListAsync(ct);
    }

    public async Task<decimal> GetSubtotalByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(bc => bc.BookingId == bookingId)
            .Select(bc => (decimal?)(bc.Quantity * bc.UnitPrice))
            .SumAsync(ct) ?? 0m;
    }

    public Task<int> DeleteByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        return DbSet
            .Where(bc => bc.BookingId == bookingId)
            .ExecuteDeleteAsync(ct);
    }
}

