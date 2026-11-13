using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

/// <summary>
/// Data access contract for role lookup queries.
/// </summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int roleId);
    Task<Role?> GetByNameAsync(string roleName);
    Task<List<string>> GetEmployeeFilterRoleNamesAsync();
    Task<List<Role>> GetAssignableEmployeeRolesAsync();
}
