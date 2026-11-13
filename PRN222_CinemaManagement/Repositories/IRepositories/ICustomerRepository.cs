using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<int?> GetActiveCustomerIdByUserIdAsync(int userId, CancellationToken ct = default);
}

