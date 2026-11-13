using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Configuration;
using PRN222_CinemaManagement.ViewModels;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>
/// Gửi email HTML xác nhận đặt vé qua SMTP kèm mã code và ảnh PNG QR
/// cho từng vé phim lẫn phiếu nhận bắp nước (nhúng CID inline + đính kèm file .png).
/// </summary>
public class BookingEmailService(
    IQrCodeService qrCodeService,
    IConfiguration configuration,
    ILogger<BookingEmailService> logger) : IBookingEmailService
{
    public async Task SendBookingConfirmationAsync(BookingDetailsViewModel booking, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(booking.Email) || !MailAddress.TryCreate(booking.Email.Trim(), out var recipient))
        {
            logger.LogWarning("Bỏ qua gửi email đơn {BookingCode} vì email khách hàng không hợp lệ.", booking.BookingCode);
            return;
        }

        var emailSection = configuration.GetSection("Email");
        var host = emailSection["Host"]?.Trim();
        var username = emailSection["Username"]?.Trim();
        var password = emailSection["Password"];
        var fromEmail = emailSection["FromEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            fromEmail = username;
        }
        var fromName = string.IsNullOrWhiteSpace(emailSection["FromName"])
            ? "Cinema Management"
            : emailSection["FromName"]!.Trim();

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail)
            || !MailAddress.TryCreate(fromEmail, out _))
        {
            logger.LogInformation(
                "Chưa cấu hình SMTP (Email:Host / Email:FromEmail). Bỏ qua gửi mail thật cho {Email} — Đơn {BookingCode}, Vé: [{Tickets}], Bắp nước: {ConcessionCode}.",
                recipient.Address,
                booking.BookingCode,
                string.Join(", ", booking.Tickets.Select(t => $"{t.SeatLabel}:{t.TicketCode}")),
                booking.ConcessionTicketCode ?? "Không có");
            return;
        }

        var port = int.TryParse(emailSection["Port"], out var parsedPort) && parsedPort > 0
            ? parsedPort
            : 587;
        var enableSsl = !bool.TryParse(emailSection["EnableSsl"], out var parsedSsl) || parsedSsl;

        using var message = BuildConfirmationMailMessage(booking, fromEmail, fromName);
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
        logger.LogInformation("Đã gửi email xác nhận đơn {BookingCode} tới {Email}.", booking.BookingCode, recipient.Address);
    }

    public MailMessage BuildConfirmationMailMessage(BookingDetailsViewModel booking, string fromEmail, string fromName)
    {
        var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName, Encoding.UTF8),
            Subject = $"[Cinema Management] Vé điện tử đơn {booking.BookingCode} - {booking.MovieTitle}",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(booking.Email.Trim(), booking.CustomerName, Encoding.UTF8));

        var linkedResources = new List<LinkedResource>();
        var html = new StringBuilder();
        var screeningVn = BookingDisplay.VietnamTime(booking.StartAt);

        html.Append("""
            <!DOCTYPE html>
            <html lang="vi">
            <head><meta charset="utf-8" /></head>
            <body style="margin:0;padding:24px;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;color:#1f2937;">
              <div style="max-width:680px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;">
                <div style="background:#0f172a;color:#ffffff;padding:24px 28px;">
                  <div style="font-size:12px;letter-spacing:0.08em;text-transform:uppercase;color:#34d399;font-weight:700;">Thanh toán &amp; đặt vé thành công</div>
            """);

        html.Append($"<h1 style=\"margin:8px 0 4px;font-size:22px;\">{Encode(booking.MovieTitle)}</h1>");
        html.Append($"<p style=\"margin:0;color:#cbd5e1;font-size:14px;\">{Encode(booking.Auditorium)} · {screeningVn:HH:mm · dd/MM/yyyy} (giờ Việt Nam)</p>");
        html.Append($"<p style=\"margin:10px 0 0;font-size:13px;color:#e2e8f0;\">Mã đơn: <strong>{Encode(booking.BookingCode)}</strong> · Khách hàng: <strong>{Encode(booking.CustomerName)}</strong></p>");
        html.Append("</div><div style=\"padding:24px 28px;\">");

        // Phần 1: Vé ghế xem phim (mỗi ghế 1 QR + 1 mã code riêng)
        html.Append($"<h2 style=\"margin:0 0 12px;font-size:17px;color:#0f172a;\">Vé xem phim ({booking.Tickets.Count} vé)</h2>");
        html.Append("<p style=\"margin:0 0 16px;font-size:13px;color:#6b7280;\">Xuất trình mã QR hoặc mã vé điện tử bên dưới tại cổng soát vé trước giờ chiếu 15 phút.</p>");

        for (var i = 0; i < booking.Tickets.Count; i++)
        {
            var ticket = booking.Tickets[i];
            var cid = $"ticket-qr-{ticket.TicketId}-{i}";
            var qrPng = qrCodeService.GeneratePng(ticket.TicketCode);

            linkedResources.Add(CreateInlinePng(qrPng, cid));
            message.Attachments.Add(CreatePngAttachment(qrPng, $"{ticket.TicketCode}.png"));

            html.Append($"""
                <div style="border:1px solid #e5e7eb;border-radius:10px;padding:16px;margin-bottom:14px;background:#f9fafb;">
                  <table role="presentation" style="width:100%;border-collapse:collapse;">
                    <tr>
                      <td style="vertical-align:top;padding-right:16px;">
                        <div style="font-size:12px;color:#6b7280;text-transform:uppercase;">Ghế ngồi</div>
                        <div style="font-size:20px;font-weight:700;color:#0f172a;margin:2px 0 8px;">{Encode(ticket.SeatLabel)}</div>
                        <div style="font-size:13px;color:#374151;margin-bottom:6px;">Mã vé: <code style="background:#e5e7eb;padding:2px 6px;border-radius:4px;font-size:13px;">{Encode(ticket.TicketCode)}</code></div>
                        <div style="font-size:14px;font-weight:600;color:#2563eb;">{ticket.Price:N0} đ</div>
                      </td>
                      <td style="width:140px;text-align:center;vertical-align:middle;">
                        <img src="cid:{cid}" alt="QR ghế {Encode(ticket.SeatLabel)}" width="130" height="130" style="display:block;margin:0 auto;border:1px solid #d1d5db;border-radius:8px;background:#ffffff;padding:4px;" />
                      </td>
                    </tr>
                  </table>
                </div>
                """);
        }

        // Phần 2: Vé bắp nước (nếu khách có chọn kèm đồ ăn/thức uống)
        if (booking.Concessions.Count > 0 && !string.IsNullOrWhiteSpace(booking.ConcessionTicketCode))
        {
            const string concessionCid = "concession-qr";
            var concessionQrPng = qrCodeService.GeneratePng(booking.ConcessionTicketCode);

            linkedResources.Add(CreateInlinePng(concessionQrPng, concessionCid));
            message.Attachments.Add(CreatePngAttachment(concessionQrPng, $"{booking.ConcessionTicketCode}.png"));

            html.Append("""
                <h2 style="margin:24px 0 12px;font-size:17px;color:#0f172a;">Vé bắp &amp; đồ uống</h2>
                <div style="border:1px solid #fde68a;border-radius:10px;padding:16px;background:#fffbeb;">
                  <table role="presentation" style="width:100%;border-collapse:collapse;">
                    <tr>
                      <td style="vertical-align:top;padding-right:16px;">
                """);
            html.Append("<ul style=\"margin:0 0 10px;padding-left:18px;font-size:14px;color:#1f2937;\">");
            foreach (var item in booking.Concessions)
            {
                html.Append($"<li style=\"margin-bottom:4px;\"><strong>{Encode(item.Name)}</strong> × {item.Quantity} — {item.Subtotal:N0} đ</li>");
            }
            html.Append("</ul>");
            html.Append($"<div style=\"font-size:13px;color:#374151;margin-bottom:6px;\">Mã phiếu bắp nước: <code style=\"background:#fef3c7;padding:2px 6px;border-radius:4px;font-size:13px;\">{Encode(booking.ConcessionTicketCode)}</code></div>");
            html.Append($"<div style=\"font-size:14px;font-weight:600;color:#b45309;\">Tổng bắp nước: {booking.ConcessionSubtotal:N0} đ</div>");
            html.Append($"""
                      </td>
                      <td style="width:140px;text-align:center;vertical-align:middle;">
                        <img src="cid:{concessionCid}" alt="QR bắp nước {Encode(booking.BookingCode)}" width="130" height="130" style="display:block;margin:0 auto;border:1px solid #fcd34d;border-radius:8px;background:#ffffff;padding:4px;" />
                      </td>
                    </tr>
                  </table>
                </div>
                """);
        }

        html.Append($"""
                <div style="margin-top:24px;padding-top:16px;border-top:1px solid #e5e7eb;display:flex;justify-content:space-between;font-size:15px;">
                  <span>Tổng đã thanh toán:</span>
                  <strong style="color:#0f172a;font-size:18px;margin-left:8px;">{booking.TotalAmount:N0} đ</strong>
                </div>
              </div>
            </div>
            </body>
            </html>
            """);

        var htmlView = AlternateView.CreateAlternateViewFromString(html.ToString(), Encoding.UTF8, MediaTypeNames.Text.Html);
        foreach (var resource in linkedResources)
        {
            htmlView.LinkedResources.Add(resource);
        }

        message.AlternateViews.Add(htmlView);
        return message;
    }

    private static LinkedResource CreateInlinePng(byte[] pngBytes, string contentId)
    {
        var stream = new MemoryStream(pngBytes, writable: false);
        return new LinkedResource(stream, "image/png")
        {
            ContentId = contentId,
            TransferEncoding = TransferEncoding.Base64
        };
    }

    private static Attachment CreatePngAttachment(byte[] pngBytes, string fileName)
    {
        var stream = new MemoryStream(pngBytes, writable: false);
        var attachment = new Attachment(stream, fileName, "image/png");
        attachment.ContentDisposition!.Inline = false;
        return attachment;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}

