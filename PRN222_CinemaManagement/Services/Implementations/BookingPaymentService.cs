using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Exceptions;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Repositories.Interfaces;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Services.Implementations;

/// <summary>Xử lý thanh toán giả lập và kết quả VNPay đã được controller xác minh chữ ký.</summary>
public class BookingPaymentService(
    IBookingRepository bookingRepository,
    IShowtimeSeatRepository showtimeSeatRepository,
    IConcessionRepository concessionRepository,
    IBookingConcessionRepository bookingConcessionRepository,
    ITicketRepository ticketRepository,
    IPaymentRepository paymentRepository,
    IBookingTicketService ticketService,
    IBookingEmailService emailService,
    IBookingSupportService support,
    IUnitOfWork unitOfWork,
    ILogger<BookingPaymentService> logger) : IBookingPaymentService
{
    public async Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        if (request.Outcome != BookingConstants.PaymentOutcomes.Success
            && request.Outcome != BookingConstants.PaymentOutcomes.Failed
            && request.Outcome != BookingConstants.PaymentOutcomes.Cancelled)
        {
            throw new BookingException("Kết quả thanh toán không hợp lệ.", 400);
        }

        if (request.Method != BookingConstants.PaymentMethods.VNPaySimulated
            && request.Method != BookingConstants.PaymentMethods.MoMoSimulated)
        {
            throw new BookingException("Phương thức thanh toán không hợp lệ.", 400);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        Booking booking;
        var resultStatus = BookingConstants.BookingStatuses.Pending;
        try
        {
            booking = await support.LockOwnedBookingAsync(customerId, request.BookingId, ct);
            if (booking.Status == BookingConstants.BookingStatuses.Confirmed)
            {
                // Bấm lần nữa chỉ trả kết quả cũ, không tạo thêm Payment hoặc Ticket.
                await transaction.CommitAsync(ct);
                return BookingConstants.BookingStatuses.Confirmed;
            }

            support.EnsurePendingBooking(booking);

            if (request.Outcome == BookingConstants.PaymentOutcomes.Cancelled)
            {
                await bookingRepository.CancelPendingBookingAsync(booking.BookingId, ct);
                await support.ReleaseHeldSeatsAsync(booking.BookingId, ct);
                resultStatus = BookingConstants.BookingStatuses.Cancelled;
            }
            else
            {
                var showtime = await support.GetOpenShowtimeAsync(booking.ShowtimeId, ct);
                if (showtime == null)
                {
                    throw new BookingException("Suất chiếu không còn mở bán.");
                }

                var heldSeats = await showtimeSeatRepository.GetHeldSeatsByBookingIdAsync(booking.BookingId, ct);
                var bookingConcessions = await bookingConcessionRepository.GetByBookingIdAsync(booking.BookingId, ct);
                var expectedTotal = heldSeats.Sum(s => s.Price)
                    + bookingConcessions.Sum(bc => bc.Quantity * bc.UnitPrice);

                if (heldSeats.Count == 0
                    || heldSeats.Any(s => s.HoldExpiresAt <= DateTime.UtcNow)
                    || expectedTotal != booking.TotalAmount)
                {
                    throw new BookingException("Ghế, bắp nước hoặc giá đơn hàng đã thay đổi. Vui lòng chọn lại.");
                }

                if (request.Outcome == BookingConstants.PaymentOutcomes.Success)
                {
                    foreach (var seat in heldSeats)
                    {
                        var now = DateTime.UtcNow;
                        var sold = await showtimeSeatRepository.MarkSeatSoldFromHeldAsync(
                            seat.ShowtimeSeatId,
                            booking.BookingId,
                            requireNotExpiredUtc: now,
                            requireActiveSeat: true,
                            ct);

                        if (sold != 1)
                        {
                            throw new BookingException("Ghế không còn được giữ cho đơn này.");
                        }

                        await ticketRepository.AddAsync(new Ticket
                        {
                            TicketCode = "TK-" + Guid.NewGuid().ToString("N"),
                            BookingId = booking.BookingId,
                            ShowtimeId = booking.ShowtimeId,
                            ShowtimeSeatId = seat.ShowtimeSeatId,
                            Price = seat.Price,
                            Status = BookingConstants.TicketStatuses.Valid,
                            IssuedAt = now
                        }, ct);
                    }

                    await DeductConcessionsStockAsync(bookingConcessions, ct);

                    // Kiểm tra lại sau khi đợi khóa, tránh phát vé khi hạn giữ vừa kết thúc.
                    var confirmed = await bookingRepository.MarkConfirmedIfValidAsync(
                        booking.BookingId,
                        DateTime.UtcNow,
                        ct);
                    if (confirmed != 1)
                    {
                        throw new BookingException("Hết thời gian thanh toán hoặc suất chiếu đã đóng.");
                    }

                    resultStatus = BookingConstants.BookingStatuses.Confirmed;
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
            if (request.Outcome == BookingConstants.PaymentOutcomes.Success)
            {
                payment.PaidAt = DateTime.UtcNow;
            }

            await paymentRepository.AddAsync(payment, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Đơn, ghế, thanh toán và vé phải cùng rollback nếu một bước thất bại.
            await transaction.RollbackAsync(CancellationToken.None);
            unitOfWork.ClearChangeTracker();
            throw;
        }

        await support.NotifySeatsChangedAsync(booking.ShowtimeId);
        if (resultStatus == BookingConstants.BookingStatuses.Confirmed)
        {
            await SendConfirmationEmailSafelyAsync(booking.BookingId, booking.BookingCode, ct);
        }

        return resultStatus;
    }

    public async Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        Booking? booking;
        var resultStatus = BookingConstants.PaymentOutcomes.Failed;

        try
        {
            booking = await bookingRepository.GetByIdWithHeldSeatsAsync(bookingId, ct);
            if (booking == null)
            {
                throw new BookingException("Không tìm thấy đơn hàng", 404);
            }

            // Nếu đơn đã Confirmed rồi thì trả về luôn (chống F5)
            if (booking.Status == BookingConstants.BookingStatuses.Confirmed)
            {
                await transaction.CommitAsync(ct);
                return BookingConstants.BookingStatuses.Confirmed;
            }

            if (booking.Status != BookingConstants.BookingStatuses.Pending || booking.ExpiresAt <= DateTime.UtcNow)
            {
                throw new BookingException("Đơn đã hết hạn hoặc không còn chờ thanh toán.");
            }

            if (isSuccess)
            {
                var now = DateTime.UtcNow;
                var heldSeats = booking.HeldShowtimeSeats
                    .Where(s => s.Status == BookingConstants.ShowtimeSeatStatuses.Held)
                    .ToList();

                if (heldSeats.Count == 0)
                {
                    throw new BookingException("Không tìm thấy ghế đang giữ cho đơn này.");
                }

                var expectedTotal = heldSeats.Sum(s => s.Price)
                    + booking.BookingConcessions.Sum(bc => bc.Quantity * bc.UnitPrice);
                if (expectedTotal != booking.TotalAmount)
                {
                    throw new BookingException("Tổng tiền đơn hàng không khớp với ghế và bắp nước đã chọn.");
                }

                foreach (var seat in heldSeats)
                {
                    var sold = await showtimeSeatRepository.MarkSeatSoldFromHeldAsync(
                        seat.ShowtimeSeatId,
                        booking.BookingId,
                        requireNotExpiredUtc: null,
                        requireActiveSeat: false,
                        ct);

                    if (sold != 1)
                    {
                        throw new BookingException("Ghế không còn được giữ cho đơn này.");
                    }

                    await ticketRepository.AddAsync(new Ticket
                    {
                        TicketCode = "TK-" + Guid.NewGuid().ToString("N"),
                        BookingId = booking.BookingId,
                        ShowtimeId = booking.ShowtimeId,
                        ShowtimeSeatId = seat.ShowtimeSeatId,
                        Price = seat.Price,
                        Status = BookingConstants.TicketStatuses.Valid,
                        IssuedAt = now
                    }, ct);
                }

                await DeductConcessionsStockAsync(booking.BookingConcessions, ct);

                booking.Status = BookingConstants.BookingStatuses.Confirmed;

                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    PaymentCode = "PM-" + Guid.NewGuid().ToString("N"),
                    Provider = BookingConstants.PaymentProviders.VNPay,
                    TransactionRef = Guid.NewGuid().ToString("N"),
                    Amount = booking.TotalAmount,
                    // Đang dùng VNPay Sandbox: lưu đúng giá trị mà CK_Payments_Method cho phép.
                    PaymentMethod = BookingConstants.PaymentMethods.VNPaySimulated,
                    PaymentStatus = BookingConstants.PaymentOutcomes.Success,
                    CreatedAt = now,
                    PaidAt = now
                };
                await paymentRepository.AddAsync(payment, ct);

                resultStatus = BookingConstants.BookingStatuses.Confirmed;
            }
            else
            {
                var failedPayment = new Payment
                {
                    BookingId = booking.BookingId,
                    PaymentCode = "PM-" + Guid.NewGuid().ToString("N"),
                    Provider = BookingConstants.PaymentProviders.VNPay,
                    TransactionRef = Guid.NewGuid().ToString("N"),
                    Amount = booking.TotalAmount,
                    PaymentMethod = BookingConstants.PaymentMethods.VNPaySimulated,
                    PaymentStatus = BookingConstants.PaymentOutcomes.Failed,
                    CreatedAt = DateTime.UtcNow
                };
                await paymentRepository.AddAsync(failedPayment, ct);
                resultStatus = BookingConstants.PaymentOutcomes.Failed;
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            unitOfWork.ClearChangeTracker();
            throw;
        }

        if (resultStatus == BookingConstants.BookingStatuses.Confirmed && booking != null)
        {
            await support.NotifySeatsChangedAsync(booking.ShowtimeId);
            await SendConfirmationEmailSafelyAsync(booking.BookingId, booking.BookingCode, ct);
        }

        return resultStatus;
    }

    private async Task SendConfirmationEmailSafelyAsync(int bookingId, string bookingCode, CancellationToken ct)
    {
        try
        {
            var details = await ticketService.GetBookingDetailsByIdAsync(bookingId, ct);
            if (details != null)
            {
                await emailService.SendBookingConfirmationAsync(details, ct);
            }
        }
        catch (Exception ex)
        {
            // Đơn hàng và vé đã commit vào DB; lỗi gửi mail không được làm hỏng kết quả thanh toán.
            logger.LogWarning(ex, "Không gửi được email xác nhận cho đơn {BookingCode}.", bookingCode);
        }
    }

    private async Task DeductConcessionsStockAsync(IEnumerable<BookingConcession> concessions, CancellationToken ct)
    {
        // Trừ tồn kho theo thứ tự ID tăng dần để tránh deadlock khi nhiều giao dịch cùng mua.
        foreach (var item in concessions.OrderBy(c => c.ConcessionItemId))
        {
            var deducted = await concessionRepository.TryDeductStockAsync(item.ConcessionItemId, item.Quantity, ct);
            if (deducted != 1)
            {
                throw new BookingException("Một số sản phẩm bắp/nước không đủ tồn kho hoặc đã ngừng bán. Vui lòng chọn lại.", 409);
            }
        }
    }
}

