using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Exceptions;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Repositories.Interfaces;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Services.Implementations;

/// <summary>Giữ ghế, hủy đơn và nhả ghế hết hạn. Mỗi thao tác ghi tự quản lý transaction qua UnitOfWork.</summary>
public class SeatBookingService(
    IBookingRepository bookingRepository,
    IShowtimeSeatRepository showtimeSeatRepository,
    IBookingConcessionRepository bookingConcessionRepository,
    IBookingSupportService support,
    IUnitOfWork unitOfWork) : ISeatBookingService
{
    public async Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var seatIds = request.ShowtimeSeatIds;
        if (request.ShowtimeId <= 0 || request.BookingId <= 0 || seatIds == null
            || seatIds.Length > BookingConstants.MaxSeatsPerBooking || seatIds.Any(id => id <= 0)
            || seatIds.Distinct().Count() != seatIds.Length)
        {
            throw new BookingException("Danh sách ghế không hợp lệ. Mỗi đơn chọn tối đa 20 ghế.", 400);
        }

        if (seatIds.Length == 0)
        {
            if (request.BookingId.HasValue)
            {
                await CancelAsync(userId, request.BookingId.Value, ct);
            }
            return new HoldResultViewModel { ServerNow = DateTime.UtcNow };
        }

        var showtime = await support.GetOpenShowtimeAsync(request.ShowtimeId, ct);
        if (showtime == null)
        {
            throw new BookingException("Suất chiếu không còn mở bán.", 404);
        }

        // Transaction đảm bảo không giữ dở một phần ghế nếu một ghế khác đã bị mua.
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        HoldResultViewModel result;
        try
        {
            Booking booking;
            if (request.BookingId.HasValue)
            {
                booking = await support.LockOwnedBookingAsync(customerId, request.BookingId.Value, ct);
                if (booking.ShowtimeId != request.ShowtimeId)
                {
                    throw new BookingException("Đơn không thuộc suất chiếu này.", 400);
                }
                support.EnsurePendingBooking(booking);
            }
            else
            {
                // SQL lưu chính xác tới giây. Bỏ phần lẻ để không làm tròn tăng hạn giữ.
                var now = DateTime.UtcNow;
                now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
                var expiresAt = now.AddMinutes(BookingConstants.HoldDurationMinutes);
                if (expiresAt > showtime.StartAt) expiresAt = support.AsUtc(showtime.StartAt);
                if (expiresAt <= DateTime.UtcNow)
                {
                    throw new BookingException("Suất chiếu đã bắt đầu. Vui lòng chọn suất khác.");
                }

                booking = new Booking
                {
                    BookingCode = "BK-" + Guid.NewGuid().ToString("N"),
                    CustomerId = customerId,
                    ShowtimeId = request.ShowtimeId,
                    BookingDate = now,
                    ExpiresAt = expiresAt,
                    BookingChannel = BookingConstants.Channels.Online,
                    Status = BookingConstants.BookingStatuses.Pending
                };
                await bookingRepository.AddAsync(booking, ct);
                await unitOfWork.SaveChangesAsync(ct);
                // Các bước sau dùng ExecuteUpdate nên không để EF ghi lại bản đơn cũ.
                unitOfWork.Detach(booking);
            }

            var selectedSeats = await showtimeSeatRepository.GetActiveSeatsForShowtimeAsync(request.ShowtimeId, seatIds, ct);
            if (selectedSeats.Count != seatIds.Length)
            {
                throw new BookingException("Có ghế không thuộc suất chiếu hoặc đang bảo trì.", 400);
            }

            // Giữ ghế theo ID tăng dần để các request khóa ghế theo cùng thứ tự.
            foreach (var seat in selectedSeats)
            {
                var updated = await showtimeSeatRepository.TryHoldSeatAsync(
                    seat.ShowtimeSeatId,
                    booking.BookingId,
                    booking.ExpiresAt,
                    DateTime.UtcNow,
                    ct);

                if (updated != 1)
                {
                    throw new BookingException("Ghế vừa được người khác giữ hoặc mua. Vui lòng chọn lại.");
                }
            }

            // Bỏ chọn ghế nào thì nhả ghế đó, không thay đổi hạn của các ghế giữ lại.
            await showtimeSeatRepository.ReleaseUnselectedHeldSeatsAsync(booking.BookingId, seatIds, ct);

            var seatTotal = selectedSeats.Sum(s => s.Price);
            var concessionTotal = await bookingConcessionRepository.GetSubtotalByBookingIdAsync(booking.BookingId, ct);
            var total = seatTotal + concessionTotal;
            var saved = await bookingRepository.UpdatePendingTotalAmountIfValidAsync(
                booking.BookingId,
                total,
                DateTime.UtcNow,
                ct);
            if (saved != 1)
            {
                throw new BookingException("Đã hết thời gian giữ ghế hoặc suất chiếu đã đóng.");
            }

            await transaction.CommitAsync(ct);
            result = new HoldResultViewModel
            {
                BookingId = booking.BookingId,
                ExpiresAt = support.AsUtc(booking.ExpiresAt),
                ServerNow = DateTime.UtcNow,
                TotalAmount = total
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            unitOfWork.ClearChangeTracker();
            throw;
        }

        // Chỉ thông báo sau khi dữ liệu được lưu thành công.
        await support.NotifySeatsChangedAsync(request.ShowtimeId);
        return result;
    }

    public async Task CancelAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        Booking booking;
        try
        {
            booking = await support.LockOwnedBookingAsync(customerId, bookingId, ct);
            if (booking.Status != BookingConstants.BookingStatuses.Pending
                && booking.Status != BookingConstants.BookingStatuses.Cancelled
                && booking.Status != BookingConstants.BookingStatuses.Expired)
            {
                throw new BookingException("Không thể hủy đơn đã thanh toán.");
            }

            await bookingRepository.CancelPendingBookingAsync(bookingId, ct);
            await support.ReleaseHeldSeatsAsync(bookingId, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            unitOfWork.ClearChangeTracker();
            throw;
        }

        await support.NotifySeatsChangedAsync(booking.ShowtimeId);
    }

    public async Task<int> ReleaseExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredBookings = await bookingRepository.GetExpiredPendingBookingsAsync(
            now,
            BookingConstants.ExpirationBatchSize,
            ct);

        var count = 0;
        foreach (var booking in expiredBookings)
        {
            // Mỗi đơn một transaction. Worker khóa đơn trước, ghế sau như thanh toán/hủy.
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            var changed = await bookingRepository.MarkExpiredIfPendingAsync(booking.BookingId, now, ct);
            if (changed == 1)
            {
                await support.ReleaseHeldSeatsAsync(booking.BookingId, ct);
                count++;
            }

            await transaction.CommitAsync(ct);
            if (changed == 1)
            {
                await support.NotifySeatsChangedAsync(booking.ShowtimeId);
            }
        }

        return count;
    }
}

