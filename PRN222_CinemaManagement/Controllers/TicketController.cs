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
    public async Task<IActionResult> CancelMyBooking(int bookingId, string? returnUrl = null)
    {
        IActionResult RedirectBack()
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            string referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("/Booking/MyBookings", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("MyBookings", "Booking");
            }
            return RedirectToAction(nameof(MyBookings));
        }

        int? userId = HttpContext.Session.GetInt32("UserId");
        var customer = await context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
            return RedirectBack();
        }

        var booking = await context.Bookings
            .Include(b => b.Screening)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Đơn vé không tồn tại hoặc bạn không có quyền hủy.";
            return RedirectBack();
        }

        // Kiểm tra quy tắc hủy vé theo TEC-06 (trước 60 phút và chưa check-in)
        var (isEligible, reason) = CancellationRules.CheckEligibility(booking);
        if (!isEligible)
        {
            TempData["ErrorMessage"] = reason;
            return RedirectBack();
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
        return RedirectBack();
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

    // ==========================================
    // 3. HOÀN TIỀN TẠI QUẦY (STAFF CASH REFUND)
    // ==========================================

    // GET: /Ticket/RefundQueue - Quản lý danh sách đơn chờ hoàn tiền & xác nhận hoàn tiền mặt tại quầy (UC-14/UC-30)
    [HttpGet]
    [SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
    public async Task<IActionResult> RefundQueue(string? searchKeyword, string? statusFilter = "Pending")
    {
        var query = context.Bookings
            .Include(b => b.Customer).ThenInclude(c => c!.User)
            .Include(b => b.Screening).ThenInclude(s => s.Movie)
            .Include(b => b.Screening).ThenInclude(s => s.Auditorium)
            .Include(b => b.Tickets).ThenInclude(t => t.ScreeningSeat).ThenInclude(ss => ss.Seat)
            .Include(b => b.Payments)
            .Where(b => (b.Status == "Cancelled" || b.Status == "Refunded")
                     && b.Payments.Any(p => p.PaymentStatus == "Success"))
            .AsQueryable();

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

        var allList = await query.OrderByDescending(b => b.BookingDate).ToListAsync();

        var viewItems = allList.Select(b =>
        {
            decimal totalPaid = b.Payments.Where(p => p.PaymentStatus == "Success").Sum(p => p.Amount);
            decimal refundPaid = b.Payments.Sum(p => p.RefundAmount);
            decimal refundDue = b.Status == "Refunded" ? 0m : Math.Max(0m, totalPaid - refundPaid);

            return new StaffRefundItemViewModel
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                BookingDate = b.BookingDate,
                CustomerName = b.Customer?.User?.FullName ?? "Khách vãng lai",
                CustomerPhone = b.Customer?.User?.PhoneNumber ?? "N/A",
                MovieTitle = b.Screening?.Movie?.Title ?? "N/A",
                AuditoriumName = b.Screening?.Auditorium?.Name ?? "",
                ScreeningStartAt = b.Screening?.StartAt ?? DateTime.MinValue,
                SeatLabels = b.Tickets.Select(t => $"{t.ScreeningSeat.Seat.RowLabel}{t.ScreeningSeat.Seat.SeatNumber}").ToList(),
                TotalAmount = b.TotalAmount,
                TotalPaid = totalPaid,
                RefundPaid = refundPaid,
                RefundDue = refundDue,
                Status = b.Status,
                BookingChannel = b.BookingChannel
            };
        }).ToList();

        int pendingCount = viewItems.Count(i => i.RefundDue > 0);
        int completedCount = viewItems.Count(i => i.Status == "Refunded" || (i.TotalPaid > 0 && i.RefundDue == 0));
        decimal totalPendingAmt = viewItems.Where(i => i.RefundDue > 0).Sum(i => i.RefundDue);
        decimal totalRefundedAmt = viewItems.Sum(i => i.RefundPaid);

        List<StaffRefundItemViewModel> filteredItems;
        if (statusFilter == "Completed")
        {
            filteredItems = viewItems.Where(i => i.Status == "Refunded" || (i.TotalPaid > 0 && i.RefundDue == 0)).ToList();
        }
        else if (statusFilter == "All")
        {
            filteredItems = viewItems;
        }
        else // default "Pending"
        {
            filteredItems = viewItems.Where(i => i.RefundDue > 0).ToList();
        }

        var model = new StaffRefundQueueViewModel
        {
            SearchKeyword = searchKeyword,
            StatusFilter = statusFilter ?? "Pending",
            PendingCount = pendingCount,
            CompletedCount = completedCount,
            TotalPendingAmount = totalPendingAmt,
            TotalRefundedAmount = totalRefundedAmt,
            Items = filteredItems
        };

        return View(model);
    }

    // POST: /Ticket/ConfirmRefund - Staff xác nhận đã hoàn tiền mặt cho khách tại quầy
    [HttpPost]
    [ValidateAntiForgeryToken]
    [SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
    public async Task<IActionResult> ConfirmRefund(int bookingId, string? returnUrl = null)
    {
        IActionResult RedirectBack()
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(RefundQueue));
        }

        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var booking = await context.Bookings
                .Include(b => b.Payments)
                .Include(b => b.Customer).ThenInclude(c => c!.User)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt vé.";
                return RedirectBack();
            }

            if (booking.Status == "Refunded")
            {
                TempData["ErrorMessage"] = $"Đơn vé '{booking.BookingCode}' đã được hoàn tiền trước đó rồi.";
                return RedirectBack();
            }

            if (booking.Status != "Cancelled")
            {
                TempData["ErrorMessage"] = $"Chỉ đơn vé đã hủy (Cancelled) mới đủ điều kiện hoàn tiền (Trạng thái hiện tại: {booking.Status}).";
                return RedirectBack();
            }

            var successPayments = booking.Payments
                .Where(p => p.PaymentStatus == "Success")
                .ToList();

            if (!successPayments.Any())
            {
                TempData["ErrorMessage"] = $"Đơn vé '{booking.BookingCode}' chưa từng thanh toán thành công, không phát sinh tiền hoàn.";
                return RedirectBack();
            }

            decimal totalPaid = successPayments.Sum(p => p.Amount);
            decimal alreadyRefunded = successPayments.Sum(p => p.RefundAmount);
            decimal refundDue = totalPaid - alreadyRefunded;

            if (refundDue <= 0)
            {
                TempData["ErrorMessage"] = $"Đơn vé '{booking.BookingCode}' không còn khoản tiền nào cần hoàn.";
                return RedirectBack();
            }

            // Cập nhật RefundAmount vào payments
            decimal remaining = refundDue;
            foreach (var p in successPayments)
            {
                decimal canRefund = p.Amount - p.RefundAmount;
                if (canRefund > 0)
                {
                    decimal add = Math.Min(remaining, canRefund);
                    p.RefundAmount += add;
                    remaining -= add;
                    if (remaining <= 0) break;
                }
            }

            // Đổi trạng thái sang Refunded
            booking.Status = "Refunded";

            await context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["SuccessMessage"] = $"Đã xác nhận hoàn tiền mặt {refundDue:N0}đ cho khách hàng (Đơn {booking.BookingCode}) thành công!";
            return RedirectBack();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            TempData["ErrorMessage"] = "Đơn vé vừa được xử lý bởi một nhân viên khác. Vui lòng tải lại trang để kiểm tra.";
            return RedirectBack();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình xác nhận hoàn tiền: " + ex.Message;
            return RedirectBack();
        }
    }
}
