using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

public class AccountController(IAccountService accountService) : Controller
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

        var result = await accountService.AuthenticateAsync(model);
        if (!result.Succeeded)
        {
            AddErrorsToModelState(result);
            return View(model);
        }

        var user = result.Data!;

        // Lưu thông tin người dùng vào Session (Chuẩn môn PRN222)
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("Role", user.RoleName);

        TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {user.FullName}.";

        return RedirectToLocal(model.ReturnUrl, user.RoleName);
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

        var vm = await accountService.GetProfileAsync(userId.Value);
        if (vm == null) return NotFound();

        return View(vm);
    }

    // GET: /Account/UpdateProfile (UC-05)
    [HttpGet]
    [SessionAuthorize]
    public async Task<IActionResult> UpdateProfile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");

        var vm = await accountService.GetUpdateProfileAsync(userId.Value);
        if (vm == null) return NotFound();

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

        var result = await accountService.UpdateProfileAsync(userId.Value, model);
        if (result.IsNotFound) return NotFound();

        if (!result.Succeeded)
        {
            AddErrorsToModelState(result);
            return View(model);
        }

        // Cập nhật lại FullName trong Session
        HttpContext.Session.SetString("FullName", result.Data!);

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

        var result = await accountService.ChangePasswordAsync(userId.Value, model);
        if (result.IsNotFound) return NotFound();

        if (!result.Succeeded)
        {
            AddErrorsToModelState(result);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
        return RedirectToAction(nameof(Profile));
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private void AddErrorsToModelState(ServiceResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }
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
