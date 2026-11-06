using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Services;

namespace PRN222_CinemaManagement.Controllers;

/// <summary>Chỉ khách hàng được thao tác; service kiểm tra lại người dùng còn hoạt động và chủ sở hữu đơn.</summary>
[SessionAuthorize(Roles = "Customer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BookingController(IOnlineBookingService bookingService) : Controller
{
    // Không nhận UserId/CustomerId từ form để tránh truy cập hoặc thanh toán hộ đơn của người khác.
    private int CurrentUserId => HttpContext.Session.GetInt32("UserId") ?? 0;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSeats([FromBody] SetSeatsRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Danh sách ghế không hợp lệ. Mỗi đơn chọn tối đa 20 ghế." });
        try
        {
            return Json(await bookingService.SetSeatsAsync(CurrentUserId, request, ct));
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int id, CancellationToken ct)
    {
        var booking = await bookingService.GetBookingAsync(CurrentUserId, id, ct);
        if (booking is null) return NotFound("Không tìm thấy đơn đặt vé của bạn.");
        if (booking.Status == "Confirmed") return RedirectToAction(nameof(Success), new { id });
        return View(booking);
    }

    /// <summary>Bước xác nhận không kéo dài hạn giữ; quyền mua vẫn được kiểm tra lại tại lúc thanh toán.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(int id, bool confirm, CancellationToken ct)
    {
        var booking = await bookingService.GetBookingAsync(CurrentUserId, id, ct);
        if (booking is null) return NotFound("Không tìm thấy đơn đặt vé của bạn.");
        if (!CanPay(booking))
        {
            TempData["ErrorMessage"] = "Đơn không còn trong thời gian giữ ghế. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Checkout), new { id });
        }
        return RedirectToAction(nameof(Payment), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Payment(int id, CancellationToken ct)
    {
        var booking = await bookingService.GetBookingAsync(CurrentUserId, id, ct);
        if (booking is null) return NotFound("Không tìm thấy đơn đặt vé của bạn.");
        if (booking.Status == "Confirmed") return RedirectToAction(nameof(Success), new { id });
        if (!CanPay(booking)) return RedirectToAction(nameof(Checkout), new { id });
        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(SimulatedPaymentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Kết quả hoặc phương thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Checkout), new { id = request.BookingId });
        }
        try
        {
            var status = await bookingService.PayAsync(CurrentUserId, request, ct);
            if (status == "Confirmed") return RedirectToAction(nameof(Success), new { id = request.BookingId });
            TempData["ErrorMessage"] = status == "Cancelled"
                ? "Đã hủy thanh toán và giải phóng ghế."
                : "Thanh toán thất bại. Bạn có thể thử lại trong thời gian giữ ghế còn lại.";
        }
        catch (BookingException ex)
        {
            if (ex.StatusCode is 403 or 404) return StatusCode(ex.StatusCode, ex.Message);
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Checkout), new { id = request.BookingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        try
        {
            await bookingService.CancelAsync(CurrentUserId, id, ct);
            TempData["SuccessMessage"] = "Đã hủy đơn và giải phóng ghế đang giữ.";
        }
        catch (BookingException ex)
        {
            if (ex.StatusCode is 403 or 404) return StatusCode(ex.StatusCode, ex.Message);
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Checkout), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Success(int id, CancellationToken ct)
    {
        var booking = await bookingService.GetBookingAsync(CurrentUserId, id, ct);
        if (booking is null) return NotFound("Không tìm thấy đơn đặt vé của bạn.");
        if (booking.Status != "Confirmed") return RedirectToAction(nameof(Checkout), new { id });
        return View(booking);
    }

    [HttpGet]
    public async Task<IActionResult> MyBookings(CancellationToken ct) =>
        View(await bookingService.GetMyBookingsAsync(CurrentUserId, ct));

    /// <summary>Ảnh QR cũng kiểm tra chủ sở hữu, không chỉ bảo vệ trang HTML chứa ảnh.</summary>
    [HttpGet]
    public async Task<IActionResult> TicketQr(int id, CancellationToken ct)
    {
        var bytes = await bookingService.GetTicketQrAsync(CurrentUserId, id, ct);
        return bytes is null ? NotFound() : File(bytes, "image/png");
    }

    private static bool CanPay(BookingDetailsViewModel booking) =>
        booking.Status == "Pending" && booking.ExpiresAt > booking.ServerNow && booking.Seats.Count > 0;
}
