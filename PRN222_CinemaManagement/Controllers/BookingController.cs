using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Services;
using System.Net;
using System.Web;

namespace PRN222_CinemaManagement.Controllers;

// Chỉ khách hàng được dùng các chức năng đặt vé.
// Service sẽ kiểm tra thêm tài khoản còn hoạt động và đơn thuộc đúng khách hàng.
[SessionAuthorize(Roles = "Customer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BookingController(IOnlineBookingService bookingService , VnPayService vnPayService , IConfiguration configuration) : Controller
{
    private int GetCurrentUserId()
    {
        // Lấy người mua từ Session, không lấy UserId/CustomerId do trình duyệt gửi.
        return HttpContext.Session.GetInt32("UserId") ?? 0;
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

    // GET: /Booking/Checkout/5 - Xem lại ghế và tổng tiền trước thanh toán.
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
            if (booking.Status == "Confirmed")
            {
                return RedirectToAction("Success", new { id });
            }
            return View(booking);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // POST: /Booking/Checkout/5 - Xác nhận đơn, không gia hạn giữ ghế.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(int id, bool confirm, CancellationToken ct)
    {
        try
        {
            var booking = await bookingService.GetBookingAsync(GetCurrentUserId(), id, ct);
            if (booking == null)
            {
                return NotFound("Không tìm thấy đơn đặt vé của bạn.");
            }
            if (!CanPay(booking))
            {
                TempData["ErrorMessage"] = "Đơn không còn trong thời gian giữ ghế. Vui lòng kiểm tra lại.";
                return RedirectToAction("Checkout", new { id });
            }
            return RedirectToAction("Payment", new { id });
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // GET: /Booking/Payment/5 - Mở màn hình thanh toán giả lập.
    [HttpGet]
    public async Task<IActionResult> Payment(int id, CancellationToken ct)
    {
        try
        {
            var booking = await bookingService.GetBookingAsync(GetCurrentUserId(), id, ct);
            if (booking == null)
            {
                return NotFound("Không tìm thấy đơn đặt vé của bạn.");
            }
            if (booking.Status == "Confirmed")
            {
                return RedirectToAction("Success", new { id });
            }
            if (!CanPay(booking))
            {
                return RedirectToAction("Checkout", new { id });
            }
            return View(booking);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    //// POST: /Booking/Pay - Xử lý thành công, thất bại hoặc hủy thanh toán giả lập.
    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> Pay(SimulatedPaymentRequest request, CancellationToken ct)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        TempData["ErrorMessage"] = "Kết quả hoặc phương thức thanh toán không hợp lệ.";
    //        return RedirectToAction("Checkout", new { id = request.BookingId });
    //    }

    //    try
    //    {
    //        var status = await bookingService.PayAsync(GetCurrentUserId(), request, ct);
    //        if (status == "Confirmed")
    //        {
    //            return RedirectToAction("Success", new { id = request.BookingId });
    //        }
    //        if (status == "Cancelled")
    //        {
    //            TempData["ErrorMessage"] = "Đã hủy thanh toán và giải phóng ghế.";
    //        }
    //        else
    //        {
    //            TempData["ErrorMessage"] = "Thanh toán thất bại. Bạn có thể thử lại trong thời gian giữ ghế còn lại.";
    //        }
    //    }
    //    catch (BookingException ex)
    //    {
    //        if (ex.StatusCode == 403 || ex.StatusCode == 404)
    //        {
    //            return StatusCode(ex.StatusCode, ex.Message);
    //        }
    //        TempData["ErrorMessage"] = ex.Message;
    //    }
    //    return RedirectToAction("Checkout", new { id = request.BookingId });
    //}



    // POST: /Booking/Pay/5 - Chuyển hướng sang VNPay
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, CancellationToken ct)
    {
        try
        {
            // Lấy thông tin booking để kiểm tra
            var booking = await bookingService.GetBookingAsync(GetCurrentUserId(), id, ct);
            if (booking == null || booking.Status != "Pending" || booking.ExpiresAt <= booking.ServerNow)
            {
                TempData["ErrorMessage"] = "Đơn không còn trong thời gian giữ ghế. Vui lòng kiểm tra lại.";
                return RedirectToAction("Checkout", new { id });
            }

            // Lấy IP (xử lý IPv6 trên localhost)
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ipAddress == "::1") ipAddress = "127.0.0.1";

            // Tạo URL thanh toán VNPay
            // vnp_OrderInfo KHÔNG dùng khoảng trắng: VNPay sandbox ký/mã hóa chuỗi có dấu cách
            // thường bị lệch và trả "Sai chữ ký" (code 70). Dùng dấu gạch dưới thay thế.
            var paymentUrl = vnPayService.CreatePaymentUrl(
                booking.BookingId,
                booking.TotalAmount,
                $"Thanh_toan_ve_xem_phim_don_hang_{booking.BookingId}",
                ipAddress
            );

            // Chuyển hướng trình duyệt sang VNPay
            return Redirect(paymentUrl);
        }
        catch (BookingException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Checkout", new { id });
        }
    }

    // GET: /Booking/VnPayReturn - Đón kết quả từ VNPay trả về
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct)
    {
        var vnpayData = Request.Query;
        var vnp_HashSecret = configuration["VnPay:HashSecret"];

        string vnp_SecureHash = vnpayData["vnp_SecureHash"].ToString();

        var vnp_Params = new SortedDictionary<string, string>();
        foreach (var key in vnpayData.Keys)
        {
            if (key.StartsWith("vnp_") && key != "vnp_SecureHash")
            {
                vnp_Params.Add(key, vnpayData[key]!);
            }
        }

        // BẮT BUỘC: Phải dùng WebUtility.UrlEncode để mã hóa lại chuỗi trước khi băm
        var queryString = string.Join("&", vnp_Params.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));

        var checkSum = vnPayService.HmacSha512(vnp_HashSecret!, queryString);

        if (!checkSum.Equals(vnp_SecureHash, StringComparison.InvariantCultureIgnoreCase))
        {
            TempData["ErrorMessage"] = "Chữ ký không hợp lệ (Dữ liệu có thể đã bị thay đổi).";
            return RedirectToAction("Index", "Home");
        }

        string vnp_ResponseCode = vnpayData["vnp_ResponseCode"].ToString();
        int bookingId = int.Parse(vnpayData["vnp_TxnRef"].ToString()!);

        bool isSuccess = vnp_ResponseCode == "00";

        try
        {
            var status = await bookingService.ConfirmPaymentAsync(bookingId, isSuccess, ct);

            if (status == "Confirmed")
            {
                TempData["SuccessMessage"] = "Thanh toán thành công!";
                return RedirectToAction("Success", new { id = bookingId });
            }
            else
            {
                TempData["ErrorMessage"] = "Thanh toán thất bại hoặc đã bị hủy.";
                return RedirectToAction("Checkout", new { id = bookingId });
            }
        }
        catch (BookingException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Index", "Home");
        }
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
        return RedirectToAction("Checkout", new { id });
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
            if (booking.Status != "Confirmed")
            {
                return RedirectToAction("Checkout", new { id });
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

    private static bool CanPay(BookingDetailsViewModel booking)
    {
        return booking.Status == "Pending" && booking.ExpiresAt > booking.ServerNow && booking.Seats.Count > 0;
    }
}
