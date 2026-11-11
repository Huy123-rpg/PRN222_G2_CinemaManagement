using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

// Giữ ghế, hủy đơn và nhả ghế hết hạn. Mỗi thao tác ghi tự quản lý transaction của mình.
public class SeatBookingService(CinemaDbContext context, BookingSupportService support)
{
    // Nhận toàn bộ lựa chọn ghế: giữ ghế mới, nhả ghế bỏ chọn và tính lại tổng tiền.
    public async Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var seatIds = request.ShowtimeSeatIds;
        if (request.ShowtimeId <= 0 || request.BookingId <= 0 || seatIds == null
            || seatIds.Length > 20 || seatIds.Any(id => id <= 0)
            || seatIds.Distinct().Count() != seatIds.Length)
        {
            throw new BookingException("Danh sách ghế không hợp lệ. Mỗi đơn chọn tối đa 20 ghế.", 400);
        }

        if (seatIds.Length == 0)
        {
            if (request.BookingId.HasValue)
                await CancelAsync(userId, request.BookingId.Value, ct);
            return new HoldResultViewModel { ServerNow = DateTime.UtcNow };
        }

        var showtime = await support.GetOpenShowtimeAsync(request.ShowtimeId, ct);
        if (showtime == null) throw new BookingException("Suất chiếu không còn mở bán.", 404);

        // Transaction đảm bảo không giữ dở một phần ghế nếu một ghế khác đã bị mua.
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        HoldResultViewModel result;
        try
        {
            Booking booking;
            if (request.BookingId.HasValue)
            {
                booking = await support.LockOwnedBookingAsync(customerId, request.BookingId.Value, ct);
                if (booking.ShowtimeId != request.ShowtimeId)
                    throw new BookingException("Đơn không thuộc suất chiếu này.", 400);
                BookingSupportService.CheckPendingBooking(booking);
            }
            else
            {
                // SQL lưu chính xác tới giây. Bỏ phần lẻ để không làm tròn tăng hạn giữ.
                var now = DateTime.UtcNow;
                now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
                var expiresAt = now.AddMinutes(5);
                if (expiresAt > showtime.StartAt) expiresAt = BookingSupportService.AsUtc(showtime.StartAt);
                if (expiresAt <= DateTime.UtcNow)
                    throw new BookingException("Suất chiếu đã bắt đầu. Vui lòng chọn suất khác.");

                booking = new Booking
                {
                    BookingCode = "BK-" + Guid.NewGuid().ToString("N"),
                    CustomerId = customerId,
                    ShowtimeId = request.ShowtimeId,
                    BookingDate = now,
                    ExpiresAt = expiresAt,
                    BookingChannel = "Online",
                    Status = "Pending"
                };
                context.Bookings.Add(booking);
                await context.SaveChangesAsync(ct);
                // Các bước sau dùng ExecuteUpdate nên không để EF ghi lại bản đơn cũ.
                context.Entry(booking).State = EntityState.Detached;
            }

            var selectedSeats = await context.ShowtimeSeats.AsNoTracking()
                .Where(s => s.ShowtimeId == request.ShowtimeId && seatIds.Contains(s.ShowtimeSeatId)
                    && s.Seat.Status == "Active" && s.Seat.SeatType.IsActive)
                .OrderBy(s => s.ShowtimeSeatId).ToListAsync(ct);
            if (selectedSeats.Count != seatIds.Length)
                throw new BookingException("Có ghế không thuộc suất chiếu hoặc đang bảo trì.", 400);

            // Giữ ghế theo ID tăng dần để các request khóa ghế theo cùng thứ tự.
            foreach (var seat in selectedSeats)
            {
                var now = DateTime.UtcNow;
                var updated = await context.ShowtimeSeats
                    .Where(s => s.ShowtimeSeatId == seat.ShowtimeSeatId
                        && s.Seat.Status == "Active" && s.Seat.SeatType.IsActive
                        && (s.Status == "Available" || (s.Status == "Held"
                            && (s.HoldExpiresAt <= now || s.HeldByBookingId == booking.BookingId))))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(s => s.Status, "Held")
                        .SetProperty(s => s.HeldByBookingId, (int?)booking.BookingId)
                        .SetProperty(s => s.HoldExpiresAt, (DateTime?)booking.ExpiresAt), ct);

                // Điều kiện và UPDATE chạy cùng lúc ở SQL. Hai người không thể cùng lấy ghế.
                if (updated != 1)
                    throw new BookingException("Ghế vừa được người khác giữ hoặc mua. Vui lòng chọn lại.");
            }

            // Bỏ chọn ghế nào thì nhả ghế đó, không thay đổi hạn của các ghế giữ lại.
            await context.ShowtimeSeats
                .Where(s => s.HeldByBookingId == booking.BookingId && s.Status == "Held"
                    && !seatIds.Contains(s.ShowtimeSeatId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Status, "Available")
                    .SetProperty(s => s.HeldByBookingId, (int?)null)
                    .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);

            var total = selectedSeats.Sum(s => s.Price);
            var checkTime = DateTime.UtcNow;
            var saved = await context.Bookings
                .Where(b => b.BookingId == booking.BookingId && b.Status == "Pending" && b.ExpiresAt > checkTime
                    && b.Showtime.Status == "Scheduled" && b.Showtime.StartAt > checkTime
                    && b.Showtime.Movie.Status == "Showing" && b.Showtime.Auditorium.Status == "Active")
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.TotalAmount, total), ct);
            if (saved != 1) throw new BookingException("Đã hết thời gian giữ ghế hoặc suất chiếu đã đóng.");

            await transaction.CommitAsync(ct);
            result = new HoldResultViewModel
            {
                BookingId = booking.BookingId,
                ExpiresAt = BookingSupportService.AsUtc(booking.ExpiresAt),
                ServerNow = DateTime.UtcNow,
                TotalAmount = total
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
        // Chỉ thông báo sau khi dữ liệu được lưu thành công.
        await support.NotifySeatsChangedAsync(request.ShowtimeId);
        return result;
    }

    // Khách hủy đơn chưa thanh toán; nhả các ghế còn thuộc đơn trong cùng transaction.
    public async Task CancelAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        Booking booking;
        try
        {
            booking = await support.LockOwnedBookingAsync(customerId, bookingId, ct);
            if (booking.Status != "Pending" && booking.Status != "Cancelled" && booking.Status != "Expired")
                throw new BookingException("Không thể hủy đơn đã thanh toán.");

            await context.Bookings.Where(b => b.BookingId == bookingId && b.Status == "Pending")
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Cancelled"), ct);
            await support.ReleaseHeldSeatsAsync(bookingId, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        await support.NotifySeatsChangedAsync(booking.ShowtimeId);
    }

    // Worker gọi định kỳ để đánh dấu đơn hết hạn và trả ghế về trạng thái trống.
    public async Task<int> ReleaseExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredBookings = await context.Bookings.AsNoTracking()
            .Where(b => b.Status == "Pending" && b.ExpiresAt <= now)
            .OrderBy(b => b.BookingId).Take(200).ToListAsync(ct);

        var count = 0;
        foreach (var booking in expiredBookings)
        {
            // Mỗi đơn một transaction. Worker khóa đơn trước, ghế sau như thanh toán/hủy.
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var changed = await context.Bookings
                .Where(b => b.BookingId == booking.BookingId && b.Status == "Pending" && b.ExpiresAt <= now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Expired"), ct);
            if (changed == 1)
            {
                await support.ReleaseHeldSeatsAsync(booking.BookingId, ct);
                count++;
            }
            await transaction.CommitAsync(ct);
            if (changed == 1) await support.NotifySeatsChangedAsync(booking.ShowtimeId);
        }
        return count;
    }
}
