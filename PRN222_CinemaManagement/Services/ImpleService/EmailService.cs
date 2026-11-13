using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>
/// Cài đặt dịch vụ gửi email OTP qua SmtpClient của .NET (chuẩn Chapter 01 Networking).
/// Tự động fallback sang logging khi chưa cấu hình tài khoản SMTP thật để hỗ trợ demo đồ án.
/// </summary>
public class EmailService(
    IConfiguration configuration,
    ILogger<EmailService> logger) : IEmailService
{
    public bool IsSmtpConfigured()
    {
        var emailSection = configuration.GetSection("Email");
        var host = emailSection["Host"]?.Trim();
        var fromEmail = emailSection["FromEmail"]?.Trim() ?? emailSection["Username"]?.Trim();

        return !string.IsNullOrWhiteSpace(host)
            && !string.IsNullOrWhiteSpace(fromEmail)
            && MailAddress.TryCreate(fromEmail, out _);
    }

    public async Task<bool> SendOtpEmailAsync(string toEmail, string fullName, string otpCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || !MailAddress.TryCreate(toEmail.Trim(), out var recipient))
        {
            logger.LogWarning("Địa chỉ email người nhận không hợp lệ: {Email}", toEmail);
            return false;
        }

        var emailSection = configuration.GetSection("Email");
        var host = emailSection["Host"]?.Trim();
        var username = emailSection["Username"]?.Trim();
        var password = emailSection["Password"]?.Replace(" ", "").Trim();
        var fromEmail = emailSection["FromEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            fromEmail = username;
        }

        var fromName = string.IsNullOrWhiteSpace(emailSection["FromName"])
            ? "Cinema Management"
            : emailSection["FromName"]!.Trim();

        // Nếu chưa cấu hình SMTP trong appsettings.json, ghi log OTP để phục vụ dev/demo
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail)
            || !MailAddress.TryCreate(fromEmail, out _))
        {
            logger.LogInformation(
                "==========================================================\n" +
                "[DEMO/DEV OTP] Chưa cấu hình SMTP. Mã xác thực đăng ký của {Email} ({FullName}) là: >>> {OtpCode} <<<\n" +
                "==========================================================",
                recipient.Address, fullName, otpCode);
            return true;
        }

        var port = int.TryParse(emailSection["Port"], out var parsedPort) && parsedPort > 0
            ? parsedPort
            : 587;
        var enableSsl = !bool.TryParse(emailSection["EnableSsl"], out var parsedSsl) || parsedSsl;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName, Encoding.UTF8),
                Subject = $"[Cinema Management] Mã xác thực đăng ký tài khoản: {otpCode}",
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(recipient.Address, fullName, Encoding.UTF8));

            var html = $"""
                <!DOCTYPE html>
                <html lang="vi">
                <head><meta charset="utf-8" /></head>
                <body style="margin:0;padding:24px;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;color:#1f2937;">
                  <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;box-shadow:0 4px 6px -1px rgba(0,0,0,0.1);">
                    <div style="background:#0f172a;color:#ffffff;padding:24px 28px;text-align:center;">
                      <h2 style="margin:0;font-size:20px;color:#38bdf8;">Cinema Management</h2>
                      <p style="margin:6px 0 0;color:#94a3b8;font-size:13px;">Hệ thống đặt vé xem phim trực tuyến</p>
                    </div>
                    <div style="padding:28px;">
                      <p style="margin:0 0 16px;font-size:15px;">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                      <p style="margin:0 0 20px;font-size:14px;color:#4b5563;line-height:1.5;">
                        Bạn đang thực hiện đăng ký tài khoản tại <strong>Cinema Management</strong>. Dưới đây là mã xác thực (OTP) của bạn:
                      </p>
                      <div style="background:#f1f5f9;border:2px dashed #0284c7;border-radius:8px;padding:18px;text-align:center;margin:20px 0;">
                        <span style="font-size:32px;font-weight:700;letter-spacing:8px;color:#0369a1;font-family:Consolas,monospace;">{otpCode}</span>
                      </div>
                      <p style="margin:0 0 8px;font-size:13px;color:#6b7280;text-align:center;">
                        Mã xác thực có hiệu lực trong vòng <strong>5 phút</strong>.
                      </p>
                      <p style="margin:0;font-size:13px;color:#9ca3af;text-align:center;">
                        Nếu bạn không yêu cầu đăng ký tài khoản này, vui lòng bỏ qua email.
                      </p>
                    </div>
                    <div style="background:#f8fafc;padding:14px;text-align:center;border-top:1px solid #e5e7eb;font-size:12px;color:#94a3b8;">
                      © 2026 Cinema Management. All rights reserved.
                    </div>
                  </div>
                </body>
                </html>
                """;

            message.Body = html;

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = 15_000
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                client.Credentials = new NetworkCredential(username, password ?? string.Empty);
            }

            await client.SendMailAsync(message, ct);
            logger.LogInformation("Đã gửi email OTP tới {Email}.", recipient.Address);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi gửi email OTP tới {Email}: {Message}", recipient.Address, ex.Message);
            // Ghi log mã OTP dự phòng để người dùng/dev không bị kẹt khi mạng lỗi
            logger.LogInformation(
                "[FALLBACK OTP] Do lỗi gửi SMTP, mã OTP dự phòng cho {Email} là: {OtpCode}",
                recipient.Address, otpCode);
            return true;
        }
    }
}
