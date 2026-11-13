using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="IRoleRepository"/>.
/// </summary>
public class RoleRepository(CinemaDbContext context) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(int roleId)
    {
        return await context.Roles.FindAsync(roleId);
    }

    public Task<List<string>> GetEmployeeFilterRoleNamesAsync()
    {
        return context.Roles
            .Where(r => r.RoleName != "Customer")
            .Select(r => r.RoleName)
            .ToListAsync();
    }

    public Task<List<Role>> GetAssignableEmployeeRolesAsync()
    {
        return context.Roles
            .Where(r => r.RoleName == "Staff" || r.RoleName == "CinemaManager")
            .ToListAsync();
    }
}
