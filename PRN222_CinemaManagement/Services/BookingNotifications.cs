using Microsoft.AspNetCore.SignalR;
using PRN222_CinemaManagement.Hubs;

namespace PRN222_CinemaManagement.Services;

public interface ISeatNotifier
{
    Task NotifyAsync(int showtimeId, CancellationToken ct = default);
}

/// <summary>Chỉ gọi sau commit: trình duyệt lấy snapshot riêng để biết ghế nào thuộc về mình.</summary>
public class SeatNotifier(IHubContext<SeatHub> hub) : ISeatNotifier
{
    public Task NotifyAsync(int showtimeId, CancellationToken ct = default) =>
        hub.Clients.Group(SeatHub.GroupName(showtimeId)).SendAsync("SeatStatusChanged", new { showtimeId }, ct);
}

public interface ITicketEmailSender
{
    Task SendAsync(string bookingCode, IReadOnlyList<string> ticketCodes, CancellationToken ct = default);
}

/// <summary>Email giả lập: ghi nhận việc phát vé trong log; không gửi email thật hay ghi PII.</summary>
public class SimulatedTicketEmailSender(ILogger<SimulatedTicketEmailSender> logger) : ITicketEmailSender
{
    public Task SendAsync(string bookingCode, IReadOnlyList<string> ticketCodes, CancellationToken ct = default)
    {
        logger.LogInformation("Email giả lập: đơn {BookingCode} đã phát {TicketCount} vé. Khách xem QR tại Vé của tôi.",
            bookingCode, ticketCodes.Count);
        return Task.CompletedTask;
    }
}
