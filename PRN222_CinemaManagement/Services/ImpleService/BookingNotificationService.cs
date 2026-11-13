using Microsoft.AspNetCore.SignalR;
using PRN222_CinemaManagement.Hubs;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

public class BookingNotificationService(
    IHubContext<SeatHub> hubContext,
    ILogger<BookingNotificationService> logger) : IBookingNotificationService
{
    public async Task NotifySeatsChangedAsync(int screeningId)
    {
        try
        {
            await hubContext.Clients.Group(SeatHub.GroupName(screeningId))
                .SendAsync("SeatStatusChanged", new { screeningId });
        }
        catch (Exception ex)
        {
            // DB đã commit: không báo thanh toán lỗi chỉ vì SignalR mất kết nối.
            // Trình duyệt vẫn có thể tải lại sơ đồ bằng HTTP.
            logger.LogWarning(ex, "Không gửi được cập nhật sơ đồ suất {ScreeningId}.", screeningId);
        }
    }
}

