using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

public class AccountController(IAccountService accountService, IEmailService emailService) : Controller
{
    private const string SessionPendingRegistration = "PendingRegistration";
    private const string SessionRegistrationOtp = "RegistrationOtp";
    private const string SessionRegistrationOtpExpiry = "RegistrationOtpExpiry";
    private const string SessionRegistrationEmail = "RegistrationEmail";

    private const string SessionResetUserId = "ResetPasswordUserId";
    private const string SessionResetEmail = "ResetPasswordEmail";
    private const string SessionResetFullName = "ResetPasswordFullName";
    private const string SessionResetOtp = "ResetPasswordOtp";
    private const string SessionResetOtpExpiry = "ResetPasswordOtpExpiry";

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

    // GET: /Account/Register (Bước 1: Form đăng ký tài khoản)
    [HttpGet]
    public IActionResult Register()
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    // POST: /Account/Register (Bước 1: Validate thông tin và gửi mã OTP qua Email)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var validateResult = await accountService.ValidateRegistrationAsync(model);
        if (!validateResult.Succeeded)
        {
            AddErrorsToModelState(validateResult);
            return View(model);
        }

        // Sinh mã OTP 6 chữ số ngẫu nhiên
        var otpCode = Random.Shared.Next(100000, 999999).ToString();

        // Lưu thông tin đăng ký và OTP vào Session (hết hạn trong 5 phút)
        HttpContext.Session.SetString(SessionPendingRegistration, JsonSerializer.Serialize(model));
        HttpContext.Session.SetString(SessionRegistrationOtp, otpCode);
        HttpContext.Session.SetString(SessionRegistrationOtpExpiry, DateTime.UtcNow.AddMinutes(5).ToString("O"));
        HttpContext.Session.SetString(SessionRegistrationEmail, model.Email.Trim());

        // Gửi email OTP
        await emailService.SendOtpEmailAsync(model.Email.Trim(), model.FullName.Trim(), otpCode);

        if (!emailService.IsSmtpConfigured())
        {
            TempData["DemoOtp"] = otpCode;
        }

        TempData["SuccessMessage"] = $"Mã xác thực đã được gửi tới email {model.Email}.";
        return RedirectToAction(nameof(VerifyEmail));
    }

    // GET: /Account/VerifyEmail (Bước 2: Form nhập mã xác thực OTP)
    [HttpGet]
    public IActionResult VerifyEmail()
    {
        var pendingJson = HttpContext.Session.GetString(SessionPendingRegistration);
        var email = HttpContext.Session.GetString(SessionRegistrationEmail);

        if (string.IsNullOrEmpty(pendingJson) || string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin đăng ký. Vui lòng thực hiện đăng ký.";
            return RedirectToAction(nameof(Register));
        }

        return View(new VerifyEmailViewModel { Email = email });
    }

    // POST: /Account/VerifyEmail (Bước 2: Xác nhận OTP và lưu tài khoản vào Database)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        var pendingJson = HttpContext.Session.GetString(SessionPendingRegistration);
        var expectedOtp = HttpContext.Session.GetString(SessionRegistrationOtp);
        var expiryStr = HttpContext.Session.GetString(SessionRegistrationOtpExpiry);
        var email = HttpContext.Session.GetString(SessionRegistrationEmail);
        model.Email = email;

        if (string.IsNullOrEmpty(pendingJson) || string.IsNullOrEmpty(expectedOtp) || string.IsNullOrEmpty(expiryStr))
        {
            TempData["ErrorMessage"] = "Phiên xác thực đã hết hạn. Vui lòng đăng ký lại.";
            return RedirectToAction(nameof(Register));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!DateTime.TryParse(expiryStr, out var expiry) || DateTime.UtcNow > expiry)
        {
            ModelState.AddModelError("OtpCode", "Mã xác thực đã hết hạn (quá 5 phút). Vui lòng nhấn 'Gửi lại mã'.");
            return View(model);
        }

        if (!string.Equals(model.OtpCode.Trim(), expectedOtp.Trim(), StringComparison.Ordinal))
        {
            ModelState.AddModelError("OtpCode", "Mã xác thực không chính xác. Vui lòng thử lại.");
            return View(model);
        }

        var registerModel = JsonSerializer.Deserialize<RegisterViewModel>(pendingJson);
        if (registerModel == null)
        {
            TempData["ErrorMessage"] = "Dữ liệu đăng ký không hợp lệ. Vui lòng đăng ký lại.";
            return RedirectToAction(nameof(Register));
        }

        var result = await accountService.RegisterAsync(registerModel);
        if (!result.Succeeded)
        {
            AddErrorsToModelState(result);
            return View(model);
        }

        // Xóa sạch dữ liệu đăng ký tạm thời khỏi Session
        HttpContext.Session.Remove(SessionPendingRegistration);
        HttpContext.Session.Remove(SessionRegistrationOtp);
        HttpContext.Session.Remove(SessionRegistrationOtpExpiry);
        HttpContext.Session.Remove(SessionRegistrationEmail);

        TempData["SuccessMessage"] = "Xác thực email và tạo tài khoản thành công! Bạn có thể đăng nhập ngay.";
        return RedirectToAction("Login", "Account");
    }

    // POST: /Account/ResendOtp (Gửi lại mã OTP mới)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        var pendingJson = HttpContext.Session.GetString(SessionPendingRegistration);
        if (string.IsNullOrEmpty(pendingJson))
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin đăng ký. Vui lòng đăng ký lại.";
            return RedirectToAction(nameof(Register));
        }

        var registerModel = JsonSerializer.Deserialize<RegisterViewModel>(pendingJson);
        if (registerModel == null)
        {
            return RedirectToAction(nameof(Register));
        }

        var newOtp = Random.Shared.Next(100000, 999999).ToString();
        HttpContext.Session.SetString(SessionRegistrationOtp, newOtp);
        HttpContext.Session.SetString(SessionRegistrationOtpExpiry, DateTime.UtcNow.AddMinutes(5).ToString("O"));

        await emailService.SendOtpEmailAsync(registerModel.Email.Trim(), registerModel.FullName.Trim(), newOtp);

        if (!emailService.IsSmtpConfigured())
        {
            TempData["DemoOtp"] = newOtp;
        }

        TempData["SuccessMessage"] = $"Đã gửi mã xác thực mới tới email {registerModel.Email}.";
        return RedirectToAction(nameof(VerifyEmail));
    }

    // GET: /Account/ForgotPassword
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new ForgotPasswordViewModel());
    }

    // POST: /Account/ForgotPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var validationResult = await accountService.ValidateForgotPasswordAsync(model.Email);
        if (!validationResult.Succeeded)
        {
            AddErrorsToModelState(validationResult);
            return View(model);
        }

        var recipient = validationResult.Data!;
        var otpCode = Random.Shared.Next(100000, 999999).ToString();

        // Lưu thông tin khôi phục vào Session (hiệu lực 5 phút)
        HttpContext.Session.SetInt32(SessionResetUserId, recipient.UserId);
        HttpContext.Session.SetString(SessionResetEmail, recipient.Email);
        HttpContext.Session.SetString(SessionResetFullName, recipient.FullName);
        HttpContext.Session.SetString(SessionResetOtp, otpCode);
        HttpContext.Session.SetString(SessionResetOtpExpiry, DateTime.UtcNow.AddMinutes(5).ToString("O"));

        await emailService.SendPasswordResetOtpEmailAsync(recipient.Email, recipient.FullName, otpCode);

        if (!emailService.IsSmtpConfigured())
        {
            TempData["DemoOtp"] = otpCode;
        }

        TempData["SuccessMessage"] = $"Mã xác thực đã được gửi tới email {recipient.Email}.";
        return RedirectToAction(nameof(ResetPassword));
    }

    // GET: /Account/ResetPassword
    [HttpGet]
    public IActionResult ResetPassword()
    {
        var email = HttpContext.Session.GetString(SessionResetEmail);
        var expectedOtp = HttpContext.Session.GetString(SessionResetOtp);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(expectedOtp))
        {
            TempData["ErrorMessage"] = "Không tìm thấy yêu cầu đặt lại mật khẩu. Vui lòng thực hiện lại.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Email = email });
    }

    // POST: /Account/ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var userId = HttpContext.Session.GetInt32(SessionResetUserId);
        var email = HttpContext.Session.GetString(SessionResetEmail);
        var expectedOtp = HttpContext.Session.GetString(SessionResetOtp);
        var expiryStr = HttpContext.Session.GetString(SessionResetOtpExpiry);
        model.Email = email;

        if (!userId.HasValue || string.IsNullOrEmpty(expectedOtp) || string.IsNullOrEmpty(expiryStr))
        {
            TempData["ErrorMessage"] = "Phiên khôi phục mật khẩu đã hết hạn. Vui lòng thực hiện lại.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!DateTime.TryParse(expiryStr, out var expiry) || DateTime.UtcNow > expiry)
        {
            ModelState.AddModelError("OtpCode", "Mã xác thực đã hết hạn (quá 5 phút). Vui lòng nhấn 'Gửi lại mã'.");
            return View(model);
        }

        if (!string.Equals(model.OtpCode.Trim(), expectedOtp.Trim(), StringComparison.Ordinal))
        {
            ModelState.AddModelError("OtpCode", "Mã xác thực không chính xác. Vui lòng thử lại.");
            return View(model);
        }

        var result = await accountService.ResetPasswordWithOtpAsync(userId.Value, model.NewPassword);
        if (!result.Succeeded)
        {
            AddErrorsToModelState(result);
            return View(model);
        }

        // Xóa Session khôi phục mật khẩu
        HttpContext.Session.Remove(SessionResetUserId);
        HttpContext.Session.Remove(SessionResetEmail);
        HttpContext.Session.Remove(SessionResetFullName);
        HttpContext.Session.Remove(SessionResetOtp);
        HttpContext.Session.Remove(SessionResetOtpExpiry);

        TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    // POST: /Account/ResendResetOtp
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendResetOtp()
    {
        var email = HttpContext.Session.GetString(SessionResetEmail);
        var fullName = HttpContext.Session.GetString(SessionResetFullName);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(fullName))
        {
            TempData["ErrorMessage"] = "Phiên xác thực đã hết hạn. Vui lòng thực hiện lại từ đầu.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var newOtp = Random.Shared.Next(100000, 999999).ToString();
        HttpContext.Session.SetString(SessionResetOtp, newOtp);
        HttpContext.Session.SetString(SessionResetOtpExpiry, DateTime.UtcNow.AddMinutes(5).ToString("O"));

        await emailService.SendPasswordResetOtpEmailAsync(email, fullName, newOtp);

        if (!emailService.IsSmtpConfigured())
        {
            TempData["DemoOtp"] = newOtp;
        }

        TempData["SuccessMessage"] = $"Đã gửi mã xác thực mới tới email {email}.";
        return RedirectToAction(nameof(ResetPassword));
    }

    // POST: /Account/Logout (UC-02 - Đăng xuất xóa Session)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        // Xóa toàn bộ dữ liệu trong Session
        HttpContext.Session.Clear();
        TempData["SuccessMessage"] = "Đăng xuất thành công.";
        return RedirectToAction("Index", "Home");
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

        if (role is "Admin" or "CinemaManager" or "Staff")
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return RedirectToAction("Index", "Home");
    }
}
