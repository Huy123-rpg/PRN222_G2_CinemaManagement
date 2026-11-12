using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>
/// Implementation of <see cref="IEmployeeService"/>.
/// </summary>
public class EmployeeService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordService passwordService) : IEmployeeService
{
    public async Task<EmployeeListViewModel> GetEmployeeListAsync(string? searchKeyword, string? roleFilter, string? statusFilter)
    {
        var users = await userRepository.GetEmployeesAsync(searchKeyword, roleFilter, statusFilter);

        var employees = users.Select(u => new EmployeeItemViewModel
        {
            UserId = u.UserId,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            RoleName = u.Role.RoleName,
            RoleId = u.RoleId,
            Status = u.Status,
            CreatedAt = u.CreatedAt,
            EmployeeCode = GetEmployeeCode(u)
        }).ToList();

        return new EmployeeListViewModel
        {
            SearchKeyword = searchKeyword,
            RoleFilter = roleFilter,
            StatusFilter = statusFilter,
            Employees = employees
        };
    }

    public Task<List<string>> GetEmployeeFilterRoleNamesAsync()
    {
        return roleRepository.GetEmployeeFilterRoleNamesAsync();
    }

    public Task<List<Role>> GetAssignableRolesAsync()
    {
        return roleRepository.GetAssignableEmployeeRolesAsync();
    }

    public async Task<ServiceResult> CreateEmployeeAsync(CreateEmployeeViewModel model, bool isModelStateValid)
    {
        var errors = new List<ValidationFailure>();

        if (await userRepository.ExistsByUsernameAsync(model.Username))
        {
            errors.Add(new ValidationFailure("Username", "Tên đăng nhập này đã tồn tại."));
        }

        if (await userRepository.ExistsByEmailAsync(model.Email))
        {
            errors.Add(new ValidationFailure("Email", "Email này đã được sử dụng."));
        }

        if (!isModelStateValid || errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        var role = await roleRepository.GetByIdAsync(model.RoleId);
        if (role == null || role.RoleName == "Customer")
        {
            return ServiceResult.Fail("RoleId", "Vai trò được chọn không hợp lệ.");
        }

        try
        {
            string createdFullName = model.FullName.Trim();
            string assignedCode = string.Empty;

            await userRepository.ExecuteInTransactionAsync(async () =>
            {
                var user = new User
                {
                    Username = model.Username.Trim(),
                    FullName = createdFullName,
                    Email = model.Email.Trim(),
                    PhoneNumber = model.PhoneNumber?.Trim(),
                    RoleId = model.RoleId,
                    Status = "Active",
                    PasswordHash = passwordService.HashPassword(model.Password),
                    CreatedAt = DateTime.UtcNow
                };

                await userRepository.AddUserAsync(user);

                // Tự sinh mã nếu để trống
                assignedCode = !string.IsNullOrWhiteSpace(model.EmployeeCode)
                    ? model.EmployeeCode.Trim()
                    : (role.RoleName == "CinemaManager" ? $"MGR{user.UserId:D4}" : $"STF{user.UserId:D4}");

                if (role.RoleName == "CinemaManager")
                {
                    userRepository.AddCinemaManager(new CinemaManager
                    {
                        UserId = user.UserId,
                        ManagerCode = assignedCode
                    });
                }
                else if (role.RoleName == "Staff")
                {
                    userRepository.AddStaff(new Staff
                    {
                        UserId = user.UserId,
                        StaffCode = assignedCode
                    });
                }

                await userRepository.SaveChangesAsync();
            });

            return ServiceResult.Success($"Tạo nhân viên '{createdFullName}' ({assignedCode}) thành công!");
        }
        catch (Exception ex)
        {
            return ServiceResult.Fail(string.Empty, "Có lỗi xảy ra khi tạo nhân viên: " + ex.Message);
        }
    }

    public async Task<EditEmployeeViewModel?> GetEmployeeForEditAsync(int id)
    {
        var user = await userRepository.GetByIdWithDetailsAsync(id);
        if (user == null || user.Role.RoleName == "Customer")
        {
            return null;
        }

        return new EditEmployeeViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            EmployeeCode = GetEmployeeCode(user),
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleId = user.RoleId,
            Status = user.Status
        };
    }

    public async Task<ServiceResult> UpdateEmployeeAsync(int id, EditEmployeeViewModel model, bool isModelStateValid)
    {
        var user = await userRepository.GetByIdWithDetailsAsync(id);
        if (user == null)
        {
            return ServiceResult.NotFound();
        }

        var errors = new List<ValidationFailure>();
        if (await userRepository.ExistsByEmailAsync(model.Email, id))
        {
            errors.Add(new ValidationFailure("Email", "Email này đã được sử dụng bởi tài khoản khác."));
        }

        if (!isModelStateValid || errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        try
        {
            await userRepository.ExecuteInTransactionAsync(async () =>
            {
                user.FullName = model.FullName.Trim();
                user.Email = model.Email.Trim();
                user.PhoneNumber = model.PhoneNumber?.Trim();
                user.Status = model.Status;

                // Xử lý đổi vai trò (UC-34: Assign employee roles)
                if (user.RoleId != model.RoleId && user.Role.RoleName != "Admin")
                {
                    var newRole = await roleRepository.GetByIdAsync(model.RoleId);
                    if (newRole != null && newRole.RoleName != "Customer" && newRole.RoleName != "Admin")
                    {
                        SwitchEmployeeRoleProfile(user, newRole.RoleName);
                        user.RoleId = model.RoleId;
                    }
                }

                await userRepository.SaveChangesAsync();
            });

            return ServiceResult.Success($"Cập nhật tài khoản nhân viên '{user.FullName}' thành công!");
        }
        catch (Exception ex)
        {
            return ServiceResult.Fail(string.Empty, "Lỗi cập nhật: " + ex.Message);
        }
    }

    public async Task<ServiceResult> ToggleEmployeeStatusAsync(int id)
    {
        var user = await userRepository.GetByIdWithDetailsAsync(id);
        if (user == null || user.Role.RoleName == "Admin")
        {
            return ServiceResult.Fail(string.Empty, "Không thể thay đổi trạng thái của tài khoản này.");
        }

        user.Status = user.Status == "Active" ? "Inactive" : "Active";
        await userRepository.SaveChangesAsync();

        return ServiceResult.Success($"Đã đổi trạng thái của {user.FullName} sang {user.Status}.");
    }

    private void SwitchEmployeeRoleProfile(User user, string targetRoleName)
    {
        if (user.Staff != null && targetRoleName == "CinemaManager")
        {
            var staffCode = user.Staff.StaffCode;
            userRepository.RemoveStaff(user.Staff);
            userRepository.AddCinemaManager(new CinemaManager
            {
                UserId = user.UserId,
                ManagerCode = "MGR" + staffCode.Replace("STF", "")
            });
        }
        else if (user.CinemaManager != null && targetRoleName == "Staff")
        {
            var managerCode = user.CinemaManager.ManagerCode;
            userRepository.RemoveCinemaManager(user.CinemaManager);
            userRepository.AddStaff(new Staff
            {
                UserId = user.UserId,
                StaffCode = "STF" + managerCode.Replace("MGR", "")
            });
        }
    }

    private static string GetEmployeeCode(User user) =>
        user.Staff?.StaffCode ??
        user.CinemaManager?.ManagerCode ??
        user.Admin?.AdminCode ??
        "-";
}
