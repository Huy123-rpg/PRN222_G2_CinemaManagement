using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Services;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

[SessionAuthorize(Roles = "Admin")]
public class EmployeeController(CinemaDbContext context, IPasswordService passwordService) : Controller
{
    // GET: /Employee (UC-31)
    [HttpGet]
    public async Task<IActionResult> Index(string? searchKeyword, string? roleFilter, string? statusFilter)
    {
        // Lấy danh sách nhân viên ngoại trừ Customer
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

        var list = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new EmployeeItemViewModel
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
                EmployeeCode = u.Staff != null ? u.Staff.StaffCode :
                               u.CinemaManager != null ? u.CinemaManager.ManagerCode :
                               u.Admin != null ? u.Admin.AdminCode : "-"
            })
            .ToListAsync();

        var viewModel = new EmployeeListViewModel
        {
            SearchKeyword = searchKeyword,
            RoleFilter = roleFilter,
            StatusFilter = statusFilter,
            Employees = list
        };

        ViewBag.Roles = await context.Roles
            .Where(r => r.RoleName != "Customer")
            .Select(r => r.RoleName)
            .ToListAsync();

        return View(viewModel);
    }

    // GET: /Employee/Create (UC-32)
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadRoleSelectListAsync();
        return View(new CreateEmployeeViewModel());
    }

    // POST: /Employee/Create (UC-32)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmployeeViewModel model)
    {
        if (await context.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError("Username", "Tên đăng nhập này đã tồn tại.");
        }

        if (await context.Users.AnyAsync(u => u.Email == model.Email))
        {
            ModelState.AddModelError("Email", "Email này đã được sử dụng.");
        }

        if (!ModelState.IsValid)
        {
            await LoadRoleSelectListAsync();
            return View(model);
        }

        var role = await context.Roles.FindAsync(model.RoleId);
        if (role == null || role.RoleName == "Customer")
        {
            ModelState.AddModelError("RoleId", "Vai trò được chọn không hợp lệ.");
            await LoadRoleSelectListAsync();
            return View(model);
        }

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                Username = model.Username.Trim(),
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                RoleId = model.RoleId,
                Status = "Active",
                PasswordHash = passwordService.HashPassword(model.Password),
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Tự sinh mã nếu để trống
            string code = !string.IsNullOrWhiteSpace(model.EmployeeCode)
                ? model.EmployeeCode.Trim()
                : (role.RoleName == "CinemaManager" ? $"MGR{user.UserId:D4}" : $"STF{user.UserId:D4}");

            if (role.RoleName == "CinemaManager")
            {
                context.CinemaManagers.Add(new CinemaManager
                {
                    UserId = user.UserId,
                    ManagerCode = code
                });
            }
            else if (role.RoleName == "Staff")
            {
                context.Staffs.Add(new Staff
                {
                    UserId = user.UserId,
                    StaffCode = code
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Tạo nhân viên '{user.FullName}' ({code}) thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi tạo nhân viên: " + ex.Message);
            await LoadRoleSelectListAsync();
            return View(model);
        }
    }

    // GET: /Employee/Edit/5 (UC-33 & UC-34)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.Staff)
            .Include(u => u.CinemaManager)
            .Include(u => u.Admin)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null || user.Role.RoleName == "Customer")
        {
            return NotFound();
        }

        string code = user.Staff != null ? user.Staff.StaffCode :
                      user.CinemaManager != null ? user.CinemaManager.ManagerCode :
                      user.Admin != null ? user.Admin.AdminCode : "-";

        var vm = new EditEmployeeViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            EmployeeCode = code,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleId = user.RoleId,
            Status = user.Status
        };

        await LoadRoleSelectListAsync(user.RoleId);
        return View(vm);
    }

    // POST: /Employee/Edit/5 (UC-33 & UC-34)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditEmployeeViewModel model)
    {
        if (id != model.UserId) return BadRequest();

        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.Staff)
            .Include(u => u.CinemaManager)
            .Include(u => u.Admin)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null) return NotFound();

        if (await context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != id))
        {
            ModelState.AddModelError("Email", "Email này đã được sử dụng bởi tài khoản khác.");
        }

        if (!ModelState.IsValid)
        {
            await LoadRoleSelectListAsync(model.RoleId);
            return View(model);
        }

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.Status = model.Status;

            // Xử lý đổi vai trò (UC-34: Assign employee roles)
            if (user.RoleId != model.RoleId && user.Role.RoleName != "Admin")
            {
                var newRole = await context.Roles.FindAsync(model.RoleId);
                if (newRole != null && newRole.RoleName != "Customer" && newRole.RoleName != "Admin")
                {
                    if (user.Staff != null && newRole.RoleName == "CinemaManager")
                    {
                        var staffCode = user.Staff.StaffCode;
                        context.Staffs.Remove(user.Staff);
                        context.CinemaManagers.Add(new CinemaManager
                        {
                            UserId = user.UserId,
                            ManagerCode = "MGR" + staffCode.Replace("STF", "")
                        });
                    }
                    else if (user.CinemaManager != null && newRole.RoleName == "Staff")
                    {
                        var managerCode = user.CinemaManager.ManagerCode;
                        context.CinemaManagers.Remove(user.CinemaManager);
                        context.Staffs.Add(new Staff
                        {
                            UserId = user.UserId,
                            StaffCode = "STF" + managerCode.Replace("MGR", "")
                        });
                    }

                    user.RoleId = model.RoleId;
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Cập nhật tài khoản nhân viên '{user.FullName}' thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Lỗi cập nhật: " + ex.Message);
            await LoadRoleSelectListAsync(model.RoleId);
            return View(model);
        }
    }

    // POST: /Employee/ToggleStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null || user.Role.RoleName == "Admin")
        {
            TempData["ErrorMessage"] = "Không thể thay đổi trạng thái của tài khoản này.";
            return RedirectToAction(nameof(Index));
        }

        user.Status = (user.Status == "Active") ? "Inactive" : "Active";
        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã đổi trạng thái của {user.FullName} sang {user.Status}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadRoleSelectListAsync(int? selectedRoleId = null)
    {
        var roles = await context.Roles
            .Where(r => r.RoleName == "Staff" || r.RoleName == "CinemaManager")
            .ToListAsync();

        ViewBag.Roles = new SelectList(roles, "RoleId", "RoleName", selectedRoleId);
    }
}
