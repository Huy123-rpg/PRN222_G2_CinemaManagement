namespace PRN222_CinemaManagement.Services.IService;

/// <summary>
/// Service gửi email hệ thống (OTP, thông báo tài khoản) qua SMTP chuẩn .NET.
/// </summary>
public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string toEmail, string fullName, string otpCode, CancellationToken ct = default);
    Task<bool> SendPasswordResetOtpEmailAsync(string toEmail, string fullName, string otpCode, CancellationToken ct = default);
    bool IsSmtpConfigured();
}
