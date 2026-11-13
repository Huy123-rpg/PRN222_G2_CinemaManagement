using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="IUserRepository"/>.
/// </summary>
public class UserRepository(CinemaDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(int userId)
    {
        return await context.Users.FindAsync(userId);
    }

    public async Task<User?> GetByUsernameWithRoleAsync(string username)
    {
        return await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByIdWithDetailsAsync(int userId)
    {
        return await context.Users
            .Include(u => u.Role)
            .Include(u => u.Admin)
            .Include(u => u.CinemaManager)
            .Include(u => u.Staff)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<List<User>> GetEmployeesAsync(string? searchKeyword, string? roleFilter, string? statusFilter)
    {
        var query = context.Users
            .Include(u => u.Role)
            .Include(u => u.Staff)
            .Include(u => u.CinemaManager)
            .Include(u => u.Admin)
            .Where(u => u.Role.RoleName != "Customer")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(keyword) ||
                u.FullName.ToLower().Contains(keyword) ||
                u.Email.ToLower().Contains(keyword) ||
                (u.Staff != null && u.Staff.StaffCode.ToLower().Contains(keyword)) ||
                (u.CinemaManager != null && u.CinemaManager.ManagerCode.ToLower().Contains(keyword)) ||
                (u.Admin != null && u.Admin.AdminCode.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            query = query.Where(u => u.Role.RoleName == roleFilter);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(u => u.Status == statusFilter);
        }

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public Task<bool> ExistsByUsernameAsync(string username)
    {
        return context.Users.AnyAsync(u => u.Username == username);
    }

    public Task<bool> ExistsByEmailAsync(string email, int? excludeUserId = null)
    {
        return context.Users.AnyAsync(u =>
            u.Email == email && (!excludeUserId.HasValue || u.UserId != excludeUserId.Value));
    }

    public async Task AddUserAsync(User user)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }

    public void AddCinemaManager(CinemaManager manager)
    {
        context.CinemaManagers.Add(manager);
    }

    public void RemoveCinemaManager(CinemaManager manager)
    {
        context.CinemaManagers.Remove(manager);
    }

    public void AddStaff(Staff staff)
    {
        context.Staffs.Add(staff);
    }

    public void RemoveStaff(Staff staff)
    {
        context.Staffs.Remove(staff);
    }

    public Task SaveChangesAsync()
    {
        return context.SaveChangesAsync();
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
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
