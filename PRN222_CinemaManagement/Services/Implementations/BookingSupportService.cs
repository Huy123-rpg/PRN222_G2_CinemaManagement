using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Exceptions;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.Interfaces;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Services.Implementations;

/// <summary>
/// Các bước dùng chung trong module đặt vé: kiểm tra khách/suất chiếu, khóa đơn, nhả ghế và thông báo SignalR.
/// Không tự mở hoặc commit transaction; mọi truy vấn chạy cùng scope/transaction với service gọi nó.
/// </summary>
public class BookingSupportService(
    ICustomerRepository customerRepository,
    IShowtimeRepository showtimeRepository,
    IBookingRepository bookingRepository,
    IShowtimeSeatRepository showtimeSeatRepository,
    IBookingNotificationService notificationService) : IBookingSupportService
{
    public async Task<int> GetActiveCustomerIdAsync(int userId, CancellationToken ct = default)
    {
        // Session có thể còn hiệu lực sau khi tài khoản bị khóa, vì vậy cần kiểm tra DB.
        var customerId = await customerRepository.GetActiveCustomerIdByUserIdAsync(userId, ct);
        if (!customerId.HasValue)
        {
            throw new BookingException("Tài khoản khách hàng không hợp lệ hoặc đã bị khóa.", 403);
        }

        return customerId.Value;
    }

    public Task<Showtime?> GetOpenShowtimeAsync(int showtimeId, CancellationToken ct = default)
        => showtimeRepository.GetOpenShowtimeByIdAsync(showtimeId, DateTime.UtcNow, ct);

    public async Task<Booking> LockOwnedBookingAsync(int customerId, int bookingId, CancellationToken ct = default)
    {
        var booking = await bookingRepository.LockAndGetOwnedOnlineBookingAsync(customerId, bookingId, ct);
        if (booking == null)
        {
            throw new BookingException("Không tìm thấy đơn đặt vé của bạn.", 404);
        }

        return booking;
    }

    public void EnsurePendingBooking(Booking booking)
    {
        if (booking.Status != BookingConstants.BookingStatuses.Pending || booking.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BookingException("Đơn đã hết hạn hoặc không còn chờ thanh toán. Vui lòng chọn ghế lại.");
        }
    }

    public Task ReleaseHeldSeatsAsync(int bookingId, CancellationToken ct = default)
        => showtimeSeatRepository.ReleaseHeldSeatsByBookingIdAsync(bookingId, ct);

    public Task NotifySeatsChangedAsync(int showtimeId)
        => notificationService.NotifySeatsChangedAsync(showtimeId);

    public DateTime AsUtc(DateTime value)
    {
        // SQL datetime2 không lưu múi giờ; gắn UTC để JSON có hậu tố Z và JS đếm đúng giờ.
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}

