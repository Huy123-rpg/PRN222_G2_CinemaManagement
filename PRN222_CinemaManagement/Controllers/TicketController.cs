using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

public class TicketController(CinemaDbContext context) : Controller
{
    // ==========================================
    // 1. DÀNH CHO KHÁCH HÀNG (CUSTOMER)
    // ==========================================

    // GET: /Ticket/MyBookings - Lịch sử đặt vé của khách hàng (UC-10)
    [HttpGet]
    [SessionAuthorize(Roles = "Customer")]
    public async Task<IActionResult> MyBookings()
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var customer = await context.Customers.FirstOrDefaultAsync(c => c.UserId == userId.Value);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
            return RedirectToAction("Index", "Home");
        }

        var bookings = await context.Bookings
            .Include(b => b.Screening)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Screening)
                .ThenInclude(s => s.Auditorium)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.ScreeningSeat)
                    .ThenInclude(ss => ss.Seat)
            .Where(b => b.CustomerId == customer.CustomerId)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();

        return View(bookings);
    }

    // GET: /Ticket/Details/{id} - Xem vé điện tử & mã QR (UC-11)
    [HttpGet]
    [SessionAuthorize]
    public async Task<IActionResult> Details(int id)
    {
        var booking = await context.Bookings
            .Include(b => b.Customer)
                .ThenInclude(c => c!.User)
            .Include(b => b.Screening)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Screening)
                .ThenInclude(s => s.Auditorium)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.ScreeningSeat)
                    .ThenInclude(ss => ss.Seat)
                        .ThenInclude(seat => seat.SeatType)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin vé.";
            return RedirectToAction("Index", "Home");
        }

        // Bảo mật: Nếu là khách hàng, chỉ được xem vé của chính mình
        string? role = HttpContext.Session.GetString("Role");
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (role == "Customer")
        {
            var customer = await context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (customer == null || booking.CustomerId != customer.CustomerId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }
        }

        return View(booking);
    }

    // POST: /Ticket/CancelMyBooking - Khách hàng tự hủy vé hợp lệ (UC-12 & TEC-06)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [SessionAuthorize(Roles = "Customer")]
    public async Task<IActionResult> CancelMyBooking(int bookingId)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        var customer = await context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
            return RedirectToAction(nameof(MyBookings));
        }

        var booking = await context.Bookings
            .Include(b => b.Screening)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Đơn vé không tồn tại hoặc bạn không có quyền hủy.";
            return RedirectToAction(nameof(MyBookings));
        }

        // Kiểm tra quy tắc hủy vé theo TEC-06 (trước 60 phút và chưa check-in)
        var (isEligible, reason) = CancellationRules.CheckEligibility(booking);
        if (!isEligible)
        {
            TempData["ErrorMessage"] = reason;
            return RedirectToAction(nameof(MyBookings));
        }

        // Bước 1: Đổi trạng thái đơn và vé sang Cancelled
        booking.Status = "Cancelled";
        foreach (var ticket in booking.Tickets)
        {
            ticket.Status = "Cancelled";
        }

        // Bước 2: Giải phóng các ghế đã đặt trong suất chiếu về Available
        var seatIds = booking.Tickets.Select(t => t.ScreeningSeatId).ToList();
        var screeningSeats = await context.ScreeningSeats
            .Where(ss => seatIds.Contains(ss.ScreeningSeatId))
            .ToListAsync();

        foreach (var ss in screeningSeats)
        {
            ss.Status = "Available";
            ss.HeldByBookingId = null;
            ss.HoldExpiresAt = null;
        }

        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã hủy đơn đặt vé thành công! Ghế của bạn đã được hoàn trả lại hệ thống.";
        return RedirectToAction(nameof(MyBookings));
    }


    // ==========================================
    // 2. DÀNH CHO NHÂN VIÊN QUẦY & QUẢN LÝ (STAFF / MANAGER / ADMIN)
    // ==========================================

    // GET: /Ticket/StaffLookup - Tra cứu đơn đặt vé theo Mã vé, Mã đơn, SĐT, Tên khách (UC-18)
    [HttpGet]
    [SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
    public async Task<IActionResult> StaffLookup(string? searchKeyword)
    {
        var query = context.Bookings
            .Include(b => b.Customer)
                .ThenInclude(c => c!.User)
            .Include(b => b.Screening)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Screening)
                .ThenInclude(s => s.Auditorium)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.ScreeningSeat)
                    .ThenInclude(ss => ss.Seat)
            .AsQueryable();

        // Sắp xếp đơn mới nhất lên đầu trước khi giới hạn số lượng
        query = query.OrderByDescending(b => b.BookingDate);

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            string kw = searchKeyword.Trim().ToLower();
            query = query.Where(b =>
                b.BookingCode.ToLower().Contains(kw) ||
                (b.Customer != null && b.Customer.User.FullName.ToLower().Contains(kw)) ||
                (b.Customer != null && b.Customer.User.PhoneNumber != null && b.Customer.User.PhoneNumber.Contains(kw)) ||
                b.Tickets.Any(t => t.TicketCode.ToLower().Contains(kw))
            );
        }
        else
        {
            // Mặc định hiển thị 20 đơn mới nhất
            query = query.Take(20);
        }

        var list = await query.ToListAsync();

        var model = new StaffLookupViewModel
        {
            SearchKeyword = searchKeyword,
            Bookings = list
        };

        return View(model);
    }

    // POST: /Ticket/StaffCancelBooking - Nhân viên hủy vé cho khách tại quầy (UC-14)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
    public async Task<IActionResult> StaffCancelBooking(int bookingId)
    {
        var booking = await context.Bookings
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn đặt vé.";
            return RedirectToAction(nameof(StaffLookup));
        }

        if (booking.Status == "Cancelled")
        {
            TempData["ErrorMessage"] = "Đơn vé này đã bị hủy trước đó.";
            return RedirectToAction(nameof(StaffLookup));
        }

        // Hủy đơn và vé
        booking.Status = "Cancelled";
        foreach (var ticket in booking.Tickets)
        {
            ticket.Status = "Cancelled";
        }

        // Trả ghế về Available
        var seatIds = booking.Tickets.Select(t => t.ScreeningSeatId).ToList();
        var screeningSeats = await context.ScreeningSeats
            .Where(ss => seatIds.Contains(ss.ScreeningSeatId))
            .ToListAsync();

        foreach (var ss in screeningSeats)
        {
            ss.Status = "Available";
            ss.HeldByBookingId = null;
            ss.HoldExpiresAt = null;
        }

        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã hủy đơn vé '{booking.BookingCode}' và hoàn trả {screeningSeats.Count} ghế vào rạp thành công!";
        return RedirectToAction(nameof(StaffLookup));
    }

    // POST: /Ticket/CheckInTicket - Xác thực mã QR và soát vé vào rạp (TEC-06)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
    public async Task<IActionResult> CheckInTicket(string ticketCode)
    {
        if (string.IsNullOrWhiteSpace(ticketCode))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập hoặc quét mã vé.";
            return RedirectToAction(nameof(StaffLookup));
        }

        var ticket = await context.Tickets
            .Include(t => t.Screening)
                .ThenInclude(s => s.Movie)
            .Include(t => t.ScreeningSeat)
                .ThenInclude(ss => ss.Seat)
            .FirstOrDefaultAsync(t => t.TicketCode == ticketCode.Trim());

        if (ticket == null)
        {
            TempData["ErrorMessage"] = $"Mã vé '{ticketCode}' không tồn tại trong hệ thống!";
            return RedirectToAction(nameof(StaffLookup));
        }

        if (ticket.Status == "Cancelled")
        {
            TempData["ErrorMessage"] = $"Vé '{ticketCode}' đã bị HỦY trước đó, không được phép vào rạp!";
            return RedirectToAction(nameof(StaffLookup));
        }

        if (ticket.Status == "CheckedIn")
        {
            TempData["ErrorMessage"] = $"Vé '{ticketCode}' đã được soát vào lúc {ticket.CheckedInAt:dd/MM/yyyy HH:mm} rồi!";
            return RedirectToAction(nameof(StaffLookup));
        }

        // Soát vé thành công
        ticket.Status = "CheckedIn";
        ticket.CheckedInAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Soát vé thành công! Ghế: {ticket.ScreeningSeat.Seat.RowLabel}{ticket.ScreeningSeat.Seat.SeatNumber} - Phim: {ticket.Screening.Movie.Title}. Mời quý khách vào phòng chiếu.";
        return RedirectToAction(nameof(StaffLookup));
    }
}
