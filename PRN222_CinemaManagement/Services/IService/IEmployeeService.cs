using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

/// <summary>
/// Business service contract for employee account listing, creation, editing, role assignment, and status toggling.
/// </summary>
public interface IEmployeeService
{
    Task<EmployeeListViewModel> GetEmployeeListAsync(string? searchKeyword, string? roleFilter, string? statusFilter);
    Task<List<string>> GetEmployeeFilterRoleNamesAsync();
    Task<List<Role>> GetAssignableRolesAsync();
    Task<ServiceResult> CreateEmployeeAsync(CreateEmployeeViewModel model, bool isModelStateValid);
    Task<EditEmployeeViewModel?> GetEmployeeForEditAsync(int id);
    Task<ServiceResult> UpdateEmployeeAsync(int id, EditEmployeeViewModel model, bool isModelStateValid);
    Task<ServiceResult> ToggleEmployeeStatusAsync(int id);
}
