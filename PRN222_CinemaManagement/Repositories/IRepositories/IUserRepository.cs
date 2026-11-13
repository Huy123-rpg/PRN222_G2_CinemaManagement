using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

/// <summary>
/// Data access contract for user, role profile, and employee management queries.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByUsernameWithRoleAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdWithDetailsAsync(int userId);
    Task<List<User>> GetEmployeesAsync(string? searchKeyword, string? roleFilter, string? statusFilter);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> ExistsByEmailAsync(string email, int? excludeUserId = null);
    Task AddUserAsync(User user);
    void AddCinemaManager(CinemaManager manager);
    void RemoveCinemaManager(CinemaManager manager);
    void AddStaff(Staff staff);
    void RemoveStaff(Staff staff);
    void AddCustomer(Customer customer);
    Task SaveChangesAsync();
    Task ExecuteInTransactionAsync(Func<Task> operation);
}
