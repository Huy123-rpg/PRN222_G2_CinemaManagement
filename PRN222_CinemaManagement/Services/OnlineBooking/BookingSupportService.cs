using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using PRN222_CinemaManagement.Hubs;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

// Các bước dùng chung: kiểm tra khách/suất chiếu, khóa đơn, nhả ghế và thông báo SignalR.
// Service này dùng cùng DbContext trong một request; không tự mở hoặc commit transaction.
public class BookingSupportService(CinemaDbContext context, IHubContext<SeatHub> hubContext,
    ILogger<BookingSupportService> logger)
{
    public async Task<int> GetActiveCustomerIdAsync(int userId, CancellationToken ct)
    {
        // Session có thể còn hiệu lực sau khi tài khoản bị khóa, vì vậy cần kiểm tra DB.
        var customer = await context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.User.Status == "Active"
                && c.User.Role.RoleName == "Customer", ct);
        if (customer == null) throw new BookingException("Tài khoản khách hàng không hợp lệ hoặc đã bị khóa.", 403);
        return customer.CustomerId;
    }

    public async Task<Showtime?> GetOpenShowtimeAsync(int showtimeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await context.Showtimes.AsNoTracking()
            .Include(s => s.Movie).Include(s => s.Auditorium)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId && s.Status == "Scheduled"
                && s.StartAt > now && s.Movie.Status == "Showing" && s.Auditorium.Status == "Active", ct);
    }

    public async Task<Booking> LockOwnedBookingAsync(int customerId, int bookingId, CancellationToken ct)
    {
        // Phải gọi trong transaction. UPDATE giữ nguyên Pending nhưng khóa dòng đơn đến commit.
        // Request thứ hai trên cùng đơn phải chờ, sau đó đọc lại trạng thái mới nhất.
        await context.Bookings
            .Where(b => b.BookingId == bookingId && b.CustomerId == customerId
                && b.BookingChannel == "Online" && b.Status == "Pending")
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Pending"), ct);

        var booking = await context.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId
                && b.BookingChannel == "Online", ct);
        if (booking == null) throw new BookingException("Không tìm thấy đơn đặt vé của bạn.", 404);
        return booking;
    }

    public static void CheckPendingBooking(Booking booking)
    {
        if (booking.Status != "Pending" || booking.ExpiresAt <= DateTime.UtcNow)
            throw new BookingException("Đơn đã hết hạn hoặc không còn chờ thanh toán. Vui lòng chọn ghế lại.");
    }

    public async Task ReleaseHeldSeatsAsync(int bookingId, CancellationToken ct)
    {
        // Không nhả ghế Sold hoặc ghế hết hạn đã được đơn khác giữ lại.
        await context.ShowtimeSeats
            .Where(s => s.HeldByBookingId == bookingId && s.Status == "Held")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, "Available")
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }

    public async Task NotifySeatsChangedAsync(int showtimeId)
    {
        try
        {
            await hubContext.Clients.Group(SeatHub.GroupName(showtimeId))
                .SendAsync("SeatStatusChanged", new { showtimeId });
        }
        catch (Exception ex)
        {
            // DB đã commit: không báo thanh toán lỗi chỉ vì SignalR mất kết nối.
            // Trình duyệt vẫn có thể tải lại sơ đồ bằng HTTP.
            logger.LogWarning(ex, "Không gửi được cập nhật sơ đồ suất {ShowtimeId}.", showtimeId);
        }
    }

    public static DateTime AsUtc(DateTime value)
    {
        // SQL datetime2 không lưu múi giờ; gắn UTC để JSON có hậu tố Z và JS đếm đúng giờ.
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
