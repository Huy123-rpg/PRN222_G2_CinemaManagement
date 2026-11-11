using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using QRCoder;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

// Đọc đơn của khách hàng, hiển thị vé và sinh ảnh QR. Không xử lý thanh toán.
public class BookingTicketService(CinemaDbContext context, BookingSupportService support)
{
    // Dữ liệu dùng chung cho Checkout, Payment và Success; chỉ lấy đơn của chính khách.
    public async Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var booking = await GetBookingDetailsQuery(customerId)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);
        if (booking == null) return null;
        return CreateBookingViewModel(booking);
    }

    // Danh sách đơn để khách mở lại vé hoặc tiếp tục thanh toán đơn còn hạn.
    public async Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var bookings = await GetBookingDetailsQuery(customerId)
            .OrderByDescending(b => b.Showtime.StartAt).ThenByDescending(b => b.BookingId).ToListAsync(ct);
        var result = new List<BookingDetailsViewModel>();
        foreach (var booking in bookings)
        {
            result.Add(CreateBookingViewModel(booking));
        }
        return result;
    }

    // Kiểm tra quyền xem vé rồi tạo ảnh PNG chứa mã vé đã lưu.
    public async Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var ticket = await context.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.Booking.CustomerId == customerId
                && t.Booking.BookingChannel == "Online" && t.Booking.Status == "Confirmed" && t.Status != "Cancelled", ct);
        if (ticket == null) return null;

        // QRCoder tạo ảnh PNG trực tiếp từ mã vé, không cần thêm interface/lớp bọc.
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticket.TicketCode, QRCodeGenerator.ECCLevel.Q);
        using var image = new PngByteQRCode(data);
        return image.GetGraphic(8);
    }

    private IQueryable<Booking> GetBookingDetailsQuery(int customerId)
    {
        // Dùng chung Include cho trang chi tiết và danh sách; Where vẫn luôn lọc chủ đơn.
        return context.Bookings.AsNoTracking().AsSplitQuery()
            .Where(b => b.CustomerId == customerId && b.BookingChannel == "Online")
            .Include(b => b.Customer).ThenInclude(c => c!.User)
            .Include(b => b.Showtime).ThenInclude(s => s.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s.Auditorium)
            .Include(b => b.HeldShowtimeSeats).ThenInclude(s => s.Seat).ThenInclude(s => s.SeatType)
            .Include(b => b.Tickets).ThenInclude(t => t.ShowtimeSeat).ThenInclude(s => s.Seat).ThenInclude(s => s.SeatType);
    }

    private static BookingDetailsViewModel CreateBookingViewModel(Booking booking)
    {
        var model = new BookingDetailsViewModel
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            ShowtimeId = booking.ShowtimeId,
            MovieTitle = booking.Showtime.Movie.Title,
            Auditorium = booking.Showtime.Auditorium.Name,
            StartAt = BookingSupportService.AsUtc(booking.Showtime.StartAt),
            Status = booking.Status,
            ExpiresAt = BookingSupportService.AsUtc(booking.ExpiresAt),
            ServerNow = DateTime.UtcNow,
            TotalAmount = booking.TotalAmount,
            CustomerName = booking.Customer!.User.FullName,
            Email = booking.Customer.User.Email
        };
        if (booking.Status == "Pending" && booking.ExpiresAt <= model.ServerNow)
            model.Status = "Expired";

        if (booking.Tickets.Count > 0)
        {
            // Sau thanh toán, liên kết giữ ghế đã xóa; lấy ghế từ vé đã phát.
            foreach (var ticket in booking.Tickets)
            {
                var seat = ticket.ShowtimeSeat.Seat;
                model.Seats.Add(new BookingSeatViewModel
                {
                    ShowtimeSeatId = ticket.ShowtimeSeatId,
                    Label = seat.RowLabel + seat.SeatNumber,
                    SeatType = seat.SeatType.TypeName,
                    Price = ticket.Price
                });
                model.Tickets.Add(new TicketViewModel
                {
                    TicketId = ticket.TicketId,
                    TicketCode = ticket.TicketCode,
                    SeatLabel = seat.RowLabel + seat.SeatNumber,
                    Price = ticket.Price,
                    Status = ticket.Status
                });
            }
        }
        else
        {
            foreach (var seat in booking.HeldShowtimeSeats)
            {
                model.Seats.Add(new BookingSeatViewModel
                {
                    ShowtimeSeatId = seat.ShowtimeSeatId,
                    Label = seat.Seat.RowLabel + seat.Seat.SeatNumber,
                    SeatType = seat.Seat.SeatType.TypeName,
                    Price = seat.Price
                });
            }
        }
        return model;
    }
}
