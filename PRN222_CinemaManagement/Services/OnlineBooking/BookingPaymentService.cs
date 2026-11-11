using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

// Xử lý thanh toán giả lập và kết quả VNPay đã được controller kiểm tra chữ ký.
public class BookingPaymentService(CinemaDbContext context, BookingSupportService support,
    ILogger<BookingPaymentService> logger)
{
    // Luồng giả lập nội bộ: khách chọn thành công, thất bại hoặc hủy, không gọi VNPay.
    public async Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        if (request.Outcome != "Success" && request.Outcome != "Failed" && request.Outcome != "Cancelled")
            throw new BookingException("Kết quả thanh toán không hợp lệ.", 400);
        if (request.Method != "VNPay_Simulated" && request.Method != "MoMo_Simulated")
            throw new BookingException("Phương thức thanh toán không hợp lệ.", 400);

        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        Booking booking;
        var resultStatus = "Pending";
        try
        {
            booking = await support.LockOwnedBookingAsync(customerId, request.BookingId, ct);
            if (booking.Status == "Confirmed")
            {
                // Bấm lần nữa chỉ trả kết quả cũ, không tạo thêm Payment hoặc Ticket.
                await transaction.CommitAsync(ct);
                return "Confirmed";
            }
            BookingSupportService.CheckPendingBooking(booking);

            if (request.Outcome == "Cancelled")
            {
                await context.Bookings.Where(b => b.BookingId == booking.BookingId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Cancelled"), ct);
                await support.ReleaseHeldSeatsAsync(booking.BookingId, ct);
                resultStatus = "Cancelled";
            }
            else
            {
                var showtime = await support.GetOpenShowtimeAsync(booking.ShowtimeId, ct);
                if (showtime == null) throw new BookingException("Suất chiếu không còn mở bán.");

                var heldSeats = await context.ShowtimeSeats.AsNoTracking()
                    .Where(s => s.HeldByBookingId == booking.BookingId && s.Status == "Held")
                    .OrderBy(s => s.ShowtimeSeatId).ToListAsync(ct);
                if (heldSeats.Count == 0 || heldSeats.Any(s => s.HoldExpiresAt <= DateTime.UtcNow)
                    || heldSeats.Sum(s => s.Price) != booking.TotalAmount)
                    throw new BookingException("Ghế hoặc giá vé đã thay đổi. Vui lòng chọn lại.");

                if (request.Outcome == "Success")
                {
                    foreach (var seat in heldSeats)
                    {
                        var now = DateTime.UtcNow;
                        var sold = await context.ShowtimeSeats
                            .Where(s => s.ShowtimeSeatId == seat.ShowtimeSeatId && s.Status == "Held"
                                && s.HeldByBookingId == booking.BookingId && s.HoldExpiresAt > now
                                && s.Seat.Status == "Active" && s.Seat.SeatType.IsActive)
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(s => s.Status, "Sold")
                                .SetProperty(s => s.HeldByBookingId, (int?)null)
                                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
                        if (sold != 1) throw new BookingException("Ghế không còn được giữ cho đơn này.");

                        context.Tickets.Add(new Ticket
                        {
                            TicketCode = "TK-" + Guid.NewGuid().ToString("N"),
                            BookingId = booking.BookingId,
                            ShowtimeId = booking.ShowtimeId,
                            ShowtimeSeatId = seat.ShowtimeSeatId,
                            Price = seat.Price,
                            Status = "Valid",
                            IssuedAt = now
                        });
                    }

                    // Kiểm tra lại sau khi đợi khóa, tránh phát vé khi hạn giữ vừa kết thúc.
                    var checkTime = DateTime.UtcNow;
                    var confirmed = await context.Bookings
                        .Where(b => b.BookingId == booking.BookingId && b.Status == "Pending" && b.ExpiresAt > checkTime
                            && b.Showtime.Status == "Scheduled" && b.Showtime.StartAt > checkTime
                            && b.Showtime.Movie.Status == "Showing" && b.Showtime.Auditorium.Status == "Active")
                        .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Confirmed"), ct);
                    if (confirmed != 1) throw new BookingException("Hết thời gian thanh toán hoặc suất chiếu đã đóng.");
                    resultStatus = "Confirmed";
                }
                // Nếu Failed, giữ nguyên Pending và hạn giữ để khách thử lại.
            }

            var payment = new Payment
            {
                BookingId = booking.BookingId,
                PaymentCode = "PM-" + Guid.NewGuid().ToString("N"),
                Provider = request.Method,
                TransactionRef = Guid.NewGuid().ToString("N"),
                Amount = booking.TotalAmount,
                PaymentMethod = request.Method,
                PaymentStatus = request.Outcome,
                CreatedAt = DateTime.UtcNow
            };
            if (request.Outcome == "Success") payment.PaidAt = DateTime.UtcNow;
            context.Payments.Add(payment);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Đơn, ghế, thanh toán và vé phải cùng rollback nếu một bước thất bại.
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }

        await support.NotifySeatsChangedAsync(booking.ShowtimeId);
        if (resultStatus == "Confirmed")
        {
            // Email chỉ giả lập qua log theo spec; khách xem QR tại trang Vé của tôi.
            logger.LogInformation("Email giả lập: đơn {BookingCode} đã thanh toán và có vé điện tử.", booking.BookingCode);
        }
        return resultStatus;
    }

    // Luồng VNPay: controller xác minh callback trước khi gọi hàm này để ghi thanh toán và phát vé.
    public async Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        Booking? booking;
        var resultStatus = "Failed";

        try
        {
            // 1. Lấy thông tin đơn hàng
            booking = await context.Bookings
                .Include(b => b.HeldShowtimeSeats)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);

            if (booking == null) throw new BookingException("Không tìm thấy đơn hàng", 404);

            // Nếu đơn đã Confirmed rồi thì trả về luôn (chống F5)
            if (booking.Status == "Confirmed")
            {
                await transaction.CommitAsync(ct);
                return "Confirmed";
            }

            if (booking.Status != "Pending" || booking.ExpiresAt <= DateTime.UtcNow)
            {
                throw new BookingException("Đơn đã hết hạn hoặc không còn chờ thanh toán.");
            }

            if (isSuccess)
            {
                // === THANH TOÁN THÀNH CÔNG ===
                var now = DateTime.UtcNow;
                var heldSeats = booking.HeldShowtimeSeats.Where(s => s.Status == "Held").ToList();

                if (heldSeats.Count == 0)
                    throw new BookingException("Không tìm thấy ghế đang giữ cho đơn này.");

                foreach (var seat in heldSeats)
                {
                    // Cập nhật ghế thành Sold
                    var sold = await context.ShowtimeSeats
                        .Where(s => s.ShowtimeSeatId == seat.ShowtimeSeatId && s.Status == "Held" && s.HeldByBookingId == booking.BookingId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(s => s.Status, "Sold")
                            .SetProperty(s => s.HeldByBookingId, (int?)null)
                            .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);

                    if (sold != 1) throw new BookingException("Ghế không còn được giữ cho đơn này.");

                    // Tạo vé
                    context.Tickets.Add(new Ticket
                    {
                        TicketCode = "TK-" + Guid.NewGuid().ToString("N"),
                        BookingId = booking.BookingId,
                        ShowtimeId = booking.ShowtimeId,
                        ShowtimeSeatId = seat.ShowtimeSeatId,
                        Price = seat.Price,
                        Status = "Valid",
                        IssuedAt = now
                    });
                }

                // Cập nhật trạng thái đơn
                booking.Status = "Confirmed";

                // Lưu lịch sử thanh toán
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    PaymentCode = "PM-" + Guid.NewGuid().ToString("N"),
                    Provider = "VNPay",
                    TransactionRef = Guid.NewGuid().ToString("N"), // Có thể lưu vnp_TransactionNo từ VNPay nếu muốn
                    Amount = booking.TotalAmount,
                    // Đang dùng VNPay Sandbox: lưu đúng giá trị mà CK_Payments_Method cho phép.
                    // Provider vẫn là VNPay; không cần sửa schema dùng chung của nhóm.
                    PaymentMethod = "VNPay_Simulated",
                    PaymentStatus = "Success",
                    CreatedAt = now,
                    PaidAt = now
                };
                context.Payments.Add(payment);

                resultStatus = "Confirmed";
            }
            else
            {
                // Thanh toán thất bại, giữ nguyên Pending để khách thử lại hoặc cho Background Worker quét
                resultStatus = "Failed";
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }

        // Báo SignalR cập nhật lại sơ đồ ghế
        if (resultStatus == "Confirmed" && booking != null)
        {
            await support.NotifySeatsChangedAsync(booking.ShowtimeId);
        }

        return resultStatus;
    }
}
