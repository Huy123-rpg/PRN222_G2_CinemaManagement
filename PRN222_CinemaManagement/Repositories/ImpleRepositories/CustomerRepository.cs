using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class CustomerRepository(CinemaDbContext context)
    : GenericRepository<Customer>(context), ICustomerRepository
{
    public async Task<int?> GetActiveCustomerIdByUserIdAsync(int userId, CancellationToken ct = default)
    {
        var customer = await DbSet
            .AsNoTracking()
            .Where(c => c.UserId == userId
                && c.User.Status == BookingConstants.ActiveUserStatus
                && c.User.Role.RoleName == BookingConstants.CustomerRole)
            .Select(c => new { c.CustomerId })
            .FirstOrDefaultAsync(ct);

        return customer?.CustomerId;
    }
}

