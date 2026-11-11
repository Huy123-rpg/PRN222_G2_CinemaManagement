using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.Interfaces;

public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<int?> GetActiveCustomerIdByUserIdAsync(int userId, CancellationToken ct = default);
}

