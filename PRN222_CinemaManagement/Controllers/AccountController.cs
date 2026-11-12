using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

public class AccountController(CinemaDbContext context, IPasswordService passwordService) : Controller
{
    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // Nếu đã đăng nhập bằng Session thì chuyển tiếp
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
        {
            return RedirectToLocal(returnUrl, HttpContext.Session.GetString("Role"));
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Account/Login (UC-01 - Đăng nhập bằng Session)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == model.Username);

        if (user == null || !passwordService.VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (user.Status != "Active")
        {
            ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa hoặc không hoạt động. Vui lòng liên hệ Admin.");
            return View(model);
        }

        // Lưu thông tin người dùng vào Session (Chuẩn môn PRN222)
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("Role", user.Role.RoleName);

        TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {user.FullName}.";

        return RedirectToLocal(model.ReturnUrl, user.Role.RoleName);
    }

    // POST: /Account/Logout (UC-02 - Đăng xuất xóa Session)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        // Xóa toàn bộ dữ liệu trong Session
        HttpContext.Session.Clear();
        TempData["SuccessMessage"] = "Đăng xuất thành công.";
        return RedirectToAction("Login", "Account");
    }

    // GET: /Account/Profile (UC-03)
    [HttpGet]
    [SessionAuthorize]
    public async Task<IActionResult> Profile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");

        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.Admin)
            .Include(u => u.CinemaManager)
            .Include(u => u.Staff)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (user == null) return NotFound();

        string? code = user.Admin?.AdminCode
            ?? user.CinemaManager?.ManagerCode
            ?? user.Staff?.StaffCode
            ?? user.Customer?.CustomerCode;

        var vm = new ProfileViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleName = user.Role.RoleName,
            UserCode = code,
            Status = user.Status,
            CreatedAt = user.CreatedAt
        };

        return View(vm);
    }

    // GET: /Account/UpdateProfile (UC-05)
    [HttpGet]
    [SessionAuthorize]
    public async Task<IActionResult> UpdateProfile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");

        var user = await context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        var vm = new UpdateProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        };

        return View(vm);
    }

    // POST: /Account/UpdateProfile (UC-05)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [SessionAuthorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");

        var user = await context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        // Kiểm tra email duy nhất với các user khác
        bool emailExists = await context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != userId.Value);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "Email này đã được sử dụng bởi tài khoản khác.");
            return View(model);
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.PhoneNumber = model.PhoneNumber;

        await context.SaveChangesAsync();

        // Cập nhật lại FullName trong Session
        HttpContext.Session.SetString("FullName", user.FullName);

        TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
        return RedirectToAction(nameof(Profile));
    }

    // GET: /Account/ChangePassword (UC-04)
    [HttpGet]
    [SessionAuthorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    // POST: /Account/ChangePassword (UC-04)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [SessionAuthorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");

        var user = await context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        if (!passwordService.VerifyPassword(model.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không chính xác.");
            return View(model);
        }

        user.PasswordHash = passwordService.HashPassword(model.NewPassword);
        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
        return RedirectToAction(nameof(Profile));
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl, string? role = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (role == "Admin")
        {
            return RedirectToAction("Index", "Employee");
        }

        return RedirectToAction("Index", "Home");
    }
}
