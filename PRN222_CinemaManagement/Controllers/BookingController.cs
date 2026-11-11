using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Exceptions;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Controllers;

// Chỉ khách hàng được dùng các chức năng đặt vé.
// Service sẽ kiểm tra thêm tài khoản còn hoạt động và đơn thuộc đúng khách hàng.
[SessionAuthorize(Roles = BookingConstants.CustomerRole)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BookingController(IOnlineBookingService bookingService, IVnPayService vnPayService) : Controller
{
    private int GetCurrentUserId()
    {
        // Lấy người mua từ Session, không lấy UserId/CustomerId do trình duyệt gửi.
        return HttpContext.Session.GetInt32(BookingConstants.UserIdSessionKey) ?? 0;
    }

    // POST: /Booking/SetSeats - Giữ hoặc bỏ chọn các ghế trong sơ đồ.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSeats([FromBody] SetSeatsRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Danh sách ghế không hợp lệ. Mỗi đơn chọn tối đa 20 ghế." });
        }

        try
        {
            var result = await bookingService.SetSeatsAsync(GetCurrentUserId(), request, ct);
            return Json(result);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    // GET: /Booking/Concessions/5 - Chọn bắp rang, đồ uống và combo sau khi giữ ghế.
    [HttpGet]
    public async Task<IActionResult> Concessions(int id, CancellationToken ct)
    {
        try
        {
            var selection = await bookingService.GetConcessionSelectionAsync(GetCurrentUserId(), id, ct);
            if (selection == null)
            {
                return NotFound("Không tìm thấy đơn đặt vé của bạn.");
            }
            if (selection.Status == BookingConstants.BookingStatuses.Confirmed)
            {
                return RedirectToAction(nameof(Success), new { id });
            }
            if (selection.Status != BookingConstants.BookingStatuses.Pending
                || selection.ExpiresAt <= selection.ServerNow
                || selection.Seats.Count == 0)
            {
                return RedirectToAction(nameof(Checkout), new { id });
            }
            return View(selection);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // POST: /Booking/Concessions/5 - Lưu bắp nước, tính tổng tiền với giá vé và tiếp tục xác nhận.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Concessions(int id, SetConcessionsRequest request, bool skipFood = false, CancellationToken ct = default)
    {
        request.BookingId = id;
        if (skipFood)
        {
            request.Items = new List<ConcessionOrderItemInput>();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = $"Số lượng mỗi món không hợp lệ (tối đa {BookingConstants.MaxQuantityPerConcessionItem} phần).";
            return RedirectToAction(nameof(Concessions), new { id });
        }

        try
        {
            await bookingService.SetConcessionsAsync(GetCurrentUserId(), request, ct);
            return RedirectToAction(nameof(Checkout), new { id });
        }
        catch (BookingException ex)
        {
            if (ex.StatusCode == 403 || ex.StatusCode == 404)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Concessions), new { id });
        }
    }

    // GET: /Booking/Checkout/5 - Xem lại ghế, bắp nước, tổng tiền và thanh toán trên cùng 1 màn hình.
    [HttpGet]
    public async Task<IActionResult> Checkout(int id, CancellationToken ct)
    {
        try
        {
            var booking = await bookingService.GetBookingAsync(GetCurrentUserId(), id, ct);
            if (booking == null)
            {
                return NotFound("Không tìm thấy đơn đặt vé của bạn.");
            }
            if (booking.Status == BookingConstants.BookingStatuses.Confirmed)
            {
                return RedirectToAction(nameof(Success), new { id });
            }
            return View(booking);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // POST: /Booking/Checkout/5 - Tương thích form cũ: xác nhận chuyển thẳng sang thanh toán VNPay.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Checkout(int id, bool confirm, CancellationToken ct)
    {
        return confirm
            ? Pay(id, ct)
            : Task.FromResult<IActionResult>(RedirectToAction(nameof(Checkout), new { id }));
    }

    // GET: /Booking/Payment/5 - Gộp chung về màn hình Xác nhận & thanh toán duy nhất.
    [HttpGet]
    public IActionResult Payment(int id)
    {
        return RedirectToAction(nameof(Checkout), new { id });
    }

    // POST: /Booking/Pay/5 - Chuyển hướng sang VNPay.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, CancellationToken ct)
    {
        try
        {
            var booking = await bookingService.GetBookingAsync(GetCurrentUserId(), id, ct);
            if (booking == null || !CanPay(booking))
            {
                TempData["ErrorMessage"] = "Đơn không còn trong thời gian giữ ghế. Vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Checkout), new { id });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ipAddress == "::1") ipAddress = "127.0.0.1";

            // vnp_OrderInfo KHÔNG dùng khoảng trắng: VNPay sandbox ký/mã hóa chuỗi có dấu cách
            // thường bị lệch và trả "Sai chữ ký" (code 70). Dùng dấu gạch dưới thay thế.
            var paymentUrl = vnPayService.CreatePaymentUrl(
                booking.BookingId,
                booking.TotalAmount,
                $"Thanh_toan_ve_xem_phim_don_hang_{booking.BookingId}",
                ipAddress);

            return Redirect(paymentUrl);
        }
        catch (BookingException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Checkout), new { id });
        }
    }

    // GET: /Booking/VnPayReturn - Đón kết quả từ VNPay trả về.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct)
    {
        var queryDictionary = Request.Query.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToString());

        var callbackResult = vnPayService.VerifyCallback(queryDictionary);
        if (!callbackResult.IsValidSignature)
        {
            TempData["PaymentFailureReason"] = callbackResult.ErrorMessage ?? "Chữ ký giao dịch VNPay không hợp lệ.";
            return RedirectToAction(nameof(PaymentFailed));
        }

        try
        {
            var status = await bookingService.ConfirmPaymentAsync(
                callbackResult.BookingId,
                callbackResult.IsSuccess,
                ct);

            if (status == BookingConstants.BookingStatuses.Confirmed)
            {
                return RedirectToAction(nameof(Success), new { id = callbackResult.BookingId });
            }

            TempData["PaymentFailureReason"] = callbackResult.ErrorMessage ?? "Giao dịch thanh toán VNPay không thành công hoặc đã bị hủy.";
            TempData["PaymentResponseCode"] = callbackResult.ResponseCode;
            TempData["PaymentTransactionNo"] = callbackResult.TransactionNo;
            return RedirectToAction(nameof(PaymentFailed), new { id = callbackResult.BookingId });
        }
        catch (BookingException ex)
        {
            TempData["PaymentFailureReason"] = ex.Message;
            TempData["PaymentResponseCode"] = callbackResult.ResponseCode;
            TempData["PaymentTransactionNo"] = callbackResult.TransactionNo;
            return callbackResult.BookingId > 0
                ? RedirectToAction(nameof(PaymentFailed), new { id = callbackResult.BookingId })
                : RedirectToAction(nameof(PaymentFailed));
        }
    }

    // GET: /Booking/PaymentFailed/5 - Trang thông báo thanh toán thất bại và cho phép thử lại khi còn giữ ghế.
    [HttpGet]
    public async Task<IActionResult> PaymentFailed(int? id, CancellationToken ct)
    {
        var failureReason = TempData["PaymentFailureReason"] as string
            ?? "Giao dịch thanh toán qua VNPay không thành công hoặc đã bị hủy.";
        var responseCode = TempData["PaymentResponseCode"] as string;
        var transactionNo = TempData["PaymentTransactionNo"] as string;

        BookingDetailsViewModel? booking = null;
        if (id.HasValue && id.Value > 0)
        {
            try
            {
                booking = await bookingService.GetBookingAsync(GetCurrentUserId(), id.Value, ct);
                if (booking == null)
                {
                    return NotFound("Không tìm thấy đơn đặt vé của bạn.");
                }
                if (booking.Status == BookingConstants.BookingStatuses.Confirmed)
                {
                    return RedirectToAction(nameof(Success), new { id = booking.BookingId });
                }
            }
            catch (BookingException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        return View(new PaymentFailedViewModel
        {
            Booking = booking,
            FailureMessage = failureReason,
            ResponseCode = responseCode,
            TransactionNo = transactionNo
        });
    }

    // POST: /Booking/Cancel/5 - Hủy đơn chưa thanh toán và nhả ghế.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        try
        {
            await bookingService.CancelAsync(GetCurrentUserId(), id, ct);
            TempData["SuccessMessage"] = "Đã hủy đơn và giải phóng ghế đang giữ.";
        }
        catch (BookingException ex)
        {
            if (ex.StatusCode == 403 || ex.StatusCode == 404)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Checkout), new { id });
    }

    // GET: /Booking/Success/5 - Xem vé sau khi thanh toán thành công.
    [HttpGet]
    public async Task<IActionResult> Success(int id, CancellationToken ct)
    {
        try
        {
            var booking = await bookingService.GetBookingAsync(GetCurrentUserId(), id, ct);
            if (booking == null)
            {
                return NotFound("Không tìm thấy đơn đặt vé của bạn.");
            }
            if (booking.Status != BookingConstants.BookingStatuses.Confirmed)
            {
                return RedirectToAction(nameof(Checkout), new { id });
            }
            return View(booking);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // GET: /Booking/MyBookings - Chỉ lấy đơn của khách hàng đang đăng nhập.
    [HttpGet]
    public async Task<IActionResult> MyBookings(CancellationToken ct)
    {
        try
        {
            var bookings = await bookingService.GetMyBookingsAsync(GetCurrentUserId(), ct);
            return View(bookings);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // GET: /Booking/TicketQr/5 - Kiểm tra quyền sở hữu cả khi tải trực tiếp ảnh QR.
    [HttpGet]
    public async Task<IActionResult> TicketQr(int id, CancellationToken ct)
    {
        try
        {
            var bytes = await bookingService.GetTicketQrAsync(GetCurrentUserId(), id, ct);
            if (bytes == null)
            {
                return NotFound();
            }
            return File(bytes, "image/png");
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // GET: /Booking/ConcessionQr/5 - Sinh ảnh QR nhận bắp và đồ uống của đơn đã xác nhận.
    [HttpGet]
    public async Task<IActionResult> ConcessionQr(int id, CancellationToken ct)
    {
        try
        {
            var bytes = await bookingService.GetConcessionQrAsync(GetCurrentUserId(), id, ct);
            if (bytes == null)
            {
                return NotFound();
            }
            return File(bytes, "image/png");
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    private static bool CanPay(BookingDetailsViewModel booking)
    {
        return booking.Status == BookingConstants.BookingStatuses.Pending
            && booking.ExpiresAt > booking.ServerNow
            && booking.Seats.Count > 0;
    }
}
