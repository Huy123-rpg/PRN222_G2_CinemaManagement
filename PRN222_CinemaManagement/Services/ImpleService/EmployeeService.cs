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
        if (role == null || (role.RoleName != "Staff" && role.RoleName != "CinemaManager"))
        {
            return ServiceResult.Fail("RoleId", "Chỉ được phép tạo tài khoản với vai trò Quản lý hoặc Nhân viên.");
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
        if (user == null || user.Role.RoleName == "Admin" || user.Role.RoleName == "Customer")
        {
            return ServiceResult.NotFound();
        }

        var errors = new List<ValidationFailure>();
        if (await userRepository.ExistsByEmailAsync(model.Email, id))
        {
            errors.Add(new ValidationFailure("Email", "Email này đã được sử dụng bởi tài khoản khác."));
        }

        // Kiểm tra vai trò hợp lệ nếu có thay đổi
        Role? targetNewRole = null;
        if (user.RoleId != model.RoleId)
        {
            targetNewRole = await roleRepository.GetByIdAsync(model.RoleId);
            if (targetNewRole == null || (targetNewRole.RoleName != "Staff" && targetNewRole.RoleName != "CinemaManager"))
            {
                errors.Add(new ValidationFailure("RoleId", "Vai trò mới không hợp lệ. Chỉ chấp nhận Quản lý hoặc Nhân viên."));
            }
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

                // Xử lý đổi vai trò (UC-34: Assign employee roles) an toàn, giữ lịch sử
                if (targetNewRole != null)
                {
                    SwitchEmployeeRoleProfile(user, targetNewRole.RoleName);
                    user.RoleId = targetNewRole.RoleId;
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
        // Giữ nguyên hồ sơ cũ để bảo toàn lịch sử bán vé (Booking.StaffId), chỉ tạo mới hồ sơ đích nếu chưa có
        if (targetRoleName == "CinemaManager")
        {
            if (user.CinemaManager == null)
            {
                var codeNum = user.Staff?.StaffCode.Replace("STF", "") ?? $"{user.UserId:D4}";
                userRepository.AddCinemaManager(new CinemaManager
                {
                    UserId = user.UserId,
                    ManagerCode = "MGR" + codeNum
                });
            }
        }
        else if (targetRoleName == "Staff")
        {
            if (user.Staff == null)
            {
                var codeNum = user.CinemaManager?.ManagerCode.Replace("MGR", "") ?? $"{user.UserId:D4}";
                userRepository.AddStaff(new Staff
                {
                    UserId = user.UserId,
                    StaffCode = "STF" + codeNum
                });
            }
        }
    }

    private static string GetEmployeeCode(User user) =>
        user.Role?.RoleName switch
        {
            "CinemaManager" => user.CinemaManager?.ManagerCode ?? user.Staff?.StaffCode ?? "-",
            "Staff" => user.Staff?.StaffCode ?? user.CinemaManager?.ManagerCode ?? "-",
            "Admin" => user.Admin?.AdminCode ?? "-",
            _ => user.Customer?.CustomerCode ?? "-"
        };
}
