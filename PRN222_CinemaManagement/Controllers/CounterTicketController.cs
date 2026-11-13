using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

/// <summary>
/// Dữ liệu đầu vào cho món bắp nước khi gửi từ form chọn ghế tại quầy.
/// </summary>
public class CounterConcessionInput
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// UC-13: Counter Ticket Sales (Bán vé tại quầy cho thu ngân kèm bắp nước)
/// Chuẩn 3-Layer MVC & Lab 2 PRN222: Controller mỏng, tiếp nhận tham số, gọi qua Service.
/// </summary>
[SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
public class CounterTicketController(ICounterTicketService counterTicketService) : Controller
{
    // GET: /CounterTicket?search=...&statusFilter=All&page=1
    // Màn hình 1: Danh sách phim dạng thẻ (Movie Catalog cho Staff) với tìm kiếm, lọc Showing / Presale và phân trang.
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? statusFilter = "All", int page = 1)
    {
        const int pageSize = 8;
        if (page < 1) page = 1;

        var catalog = await counterTicketService.GetCounterMovieCatalogAsync(search, statusFilter, page, pageSize);
        return View(catalog);
    }

    // GET: /CounterTicket/Screenings?movieId=5&showDate=2026-11-13
    // Màn hình 2: Danh sách các suất chiếu sắp tới của phim được chọn kèm bộ lọc ngày chiếu.
    [HttpGet]
    public async Task<IActionResult> Screenings(int movieId, string? showDate)
    {
        var model = await counterTicketService.GetMovieScreeningsForCounterAsync(movieId, showDate);
        if (model == null)
        {
            TempData["ErrorMessage"] = "Phim không tồn tại hoặc hiện không có suất chiếu khả dụng nào tại rạp.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    // GET: /CounterTicket/History?searchCode=...&fromDate=...&toDate=...&page=1
    // Màn hình Lịch sử bán vé tại quầy của nhân viên đang đăng nhập.
    [HttpGet]
    public async Task<IActionResult> History(string? searchCode, string? fromDate, string? toDate, int page = 1)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        const int pageSize = 10;
        if (page < 1) page = 1;

        var history = await counterTicketService.GetCounterSalesHistoryAsync(userId.Value, searchCode, fromDate, toDate, page, pageSize);
        return View(history);
    }

    // GET: /CounterTicket/SelectSeats?screeningId=5
    // Hiển thị sơ đồ ghế của suất chiếu và danh sách bắp nước có thể mua kèm.
    [HttpGet]
    public async Task<IActionResult> SelectSeats(int screeningId)
    {
        var (screening, seats) = await counterTicketService.GetScreeningSeatsMapAsync(screeningId);

        if (screening == null)
        {
            TempData["ErrorMessage"] = "Suất chiếu không tồn tại, đã bắt đầu hoặc phòng chiếu đang tạm dừng.";
            return RedirectToAction(nameof(Index));
        }

        // Tạo FormToken lưu trong Session để chống double-submit ở phía Server
        string formToken = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("CounterTicketFormToken", formToken);
        ViewBag.FormToken = formToken;
        ViewBag.HubUrl = "/hubs/seats";

        // Kiểm tra xem session có đang giữ ghế nào cho suất chiếu này không (phục vụ reload F5)
        int? holdingBookingId = HttpContext.Session.GetInt32($"CounterHoldingBookingId_{screeningId}");
        DateTime? holdingExpiresAt = null;
        if (holdingBookingId.HasValue)
        {
            var heldBooking = await counterTicketService.GetActivePendingCounterBookingAsync(holdingBookingId.Value);
            if (heldBooking != null && heldBooking.ScreeningId == screeningId && heldBooking.ExpiresAt > DateTime.UtcNow)
            {
                holdingExpiresAt = heldBooking.ExpiresAt;
            }
            else
            {
                HttpContext.Session.Remove($"CounterHoldingBookingId_{screeningId}");
                holdingBookingId = null;
            }
        }

        ViewBag.HoldingBookingId = holdingBookingId;
        ViewBag.HoldingExpiresAt = holdingExpiresAt;
        ViewBag.Screening = screening;
        ViewBag.ConcessionItems = await counterTicketService.GetAvailableConcessionsAsync();
        return View(seats);
    }

    // POST: /CounterTicket/HoldSeats
    // Thu ngân giữ hoặc nhả ghế tạm thời 5 phút tại quầy (đồng bộ nguyên tử với Online Booking).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HoldSeats([FromBody] SetSeatsRequest request, CancellationToken ct)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại." });
        }

        if (request.ScreeningSeatIds == null || request.ScreeningSeatIds.Length > BookingConstants.MaxSeatsPerBooking)
        {
            return BadRequest(new { message = $"Mỗi lần chọn tối đa {BookingConstants.MaxSeatsPerBooking} ghế." });
        }

        var result = await counterTicketService.HoldCounterSeatsAsync(
            userId.Value,
            request.ScreeningId,
            request.BookingId,
            request.ScreeningSeatIds,
            ct);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Errors.FirstOrDefault()?.Message ?? "Không thể giữ ghế." });
        }

        if (result.Data?.BookingId > 0 && request.ScreeningSeatIds != null && request.ScreeningSeatIds.Length > 0)
        {
            HttpContext.Session.SetInt32($"CounterHoldingBookingId_{request.ScreeningId}", result.Data.BookingId.Value);
        }
        else
        {
            HttpContext.Session.Remove($"CounterHoldingBookingId_{request.ScreeningId}");
        }

        return Json(result.Data);
    }

    // POST: /CounterTicket/ReleaseSeats
    // Giải phóng toàn bộ ghế đang giữ tại quầy khi hủy hoặc rời trang
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseSeats([FromBody] SetSeatsRequest request, CancellationToken ct)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue || !request.BookingId.HasValue)
        {
            return Ok();
        }

        await counterTicketService.ReleaseCounterSeatsAsync(userId.Value, request.BookingId.Value, ct);
        HttpContext.Session.Remove($"CounterHoldingBookingId_{request.ScreeningId}");
        return Ok();
    }

    // POST: /CounterTicket/Sell
    // Thu ngân xác nhận thu tiền và bán vé (kèm bắp nước nếu khách có mua).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sell(
        int screeningId,
        List<int>? seatIds,
        List<CounterConcessionInput>? concessions,
        string? formToken,
        string? paymentMethod = "Cash",
        int? holdingBookingId = null)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // 1. Chống double-submit ở phía Server qua FormToken
        string? sessionToken = HttpContext.Session.GetString("CounterTicketFormToken");
        if (string.IsNullOrEmpty(sessionToken) || sessionToken != formToken)
        {
            TempData["ErrorMessage"] = "Yêu cầu đã được xử lý hoặc biểu mẫu đã hết hạn. Vui lòng không gửi trùng lặp.";
            return RedirectToAction(nameof(SelectSeats), new { screeningId });
        }
        // Xóa token ngay lập tức để request thứ hai (nếu gửi đồng thời) bị chặn ngay
        HttpContext.Session.Remove("CounterTicketFormToken");

        // 2. Kiểm tra danh sách ghế
        if (seatIds == null || seatIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một ghế để bán vé.";
            return RedirectToAction(nameof(SelectSeats), new { screeningId });
        }

        // 3. Kiểm tra validation danh sách bắp nước
        if (concessions != null)
        {
            // Bắt buộc từ chối nếu có số lượng âm
            if (concessions.Any(c => c.Quantity < 0))
            {
                TempData["ErrorMessage"] = "Số lượng bắp nước không thể là số âm.";
                return RedirectToAction(nameof(SelectSeats), new { screeningId });
            }

            // Kiểm tra mã sản phẩm không hợp lệ
            if (concessions.Any(c => c.Quantity > 0 && c.ItemId <= 0))
            {
                TempData["ErrorMessage"] = "Mã sản phẩm bắp nước không hợp lệ.";
                return RedirectToAction(nameof(SelectSeats), new { screeningId });
            }

            // Kiểm tra trùng lặp mã sản phẩm
            if (concessions.Where(c => c.Quantity > 0).GroupBy(c => c.ItemId).Any(g => g.Count() > 1))
            {
                TempData["ErrorMessage"] = "Phát hiện món bắp nước bị trùng lặp.";
                return RedirectToAction(nameof(SelectSeats), new { screeningId });
            }
        }

        // Lọc các món bắp nước khách thực sự mua (Quantity > 0). Món Quantity = 0 được hiểu là không mua.
        var concessionOrders = concessions?
            .Where(c => c.Quantity > 0)
            .Select(c => new CounterConcessionOrder(c.ItemId, c.Quantity))
            .ToList();

        var request = new SellCounterTicketsRequest(
            userId.Value,
            screeningId,
            seatIds,
            concessionOrders,
            paymentMethod ?? "Cash",
            holdingBookingId);

        var result = await counterTicketService.SellTicketsAsync(request);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Có lỗi xảy ra trong quá trình bán vé.";
            return RedirectToAction(nameof(SelectSeats), new { screeningId });
        }

        TempData["SuccessMessage"] = result.Message;
        HttpContext.Session.Remove($"CounterHoldingBookingId_{screeningId}");

        // Chỉ cần truyền BookingId, Service/Repository tự động tìm đúng hóa đơn bắp nước cùng giao dịch
        return RedirectToAction(nameof(Receipt), new { id = result.Data!.Booking.BookingId });
    }

    // GET: /CounterTicket/SeatSnapshot?screeningId=5&bookingId=123
    // Cung cấp dữ liệu sơ đồ ghế mới nhất cho quầy và phân biệt chính xác ghế do chính quầy này giữ
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SeatSnapshot(int screeningId, int? bookingId)
    {
        var (screening, seats) = await counterTicketService.GetScreeningSeatsMapAsync(screeningId);
        if (screening == null)
        {
            return NotFound(new { message = "Suất chiếu không tồn tại hoặc đã đóng." });
        }

        int? effectiveBookingId = bookingId ?? HttpContext.Session.GetInt32($"CounterHoldingBookingId_{screeningId}");
        DateTime? expiresAt = null;
        if (effectiveBookingId.HasValue)
        {
            var heldBooking = await counterTicketService.GetActivePendingCounterBookingAsync(effectiveBookingId.Value);
            if (heldBooking != null && heldBooking.ScreeningId == screeningId && heldBooking.ExpiresAt > DateTime.UtcNow)
            {
                expiresAt = heldBooking.ExpiresAt;
            }
            else
            {
                effectiveBookingId = null;
            }
        }

        var result = new
        {
            screeningId,
            bookingId = effectiveBookingId,
            expiresAt,
            serverNow = DateTime.UtcNow,
            seats = seats.Select(s => new
            {
                screeningSeatId = s.ScreeningSeatId,
                rowLabel = s.Seat.RowLabel,
                seatNumber = s.Seat.SeatNumber,
                seatType = s.Seat.SeatType.TypeName,
                price = s.Price,
                status = s.Status,
                isMine = s.Status == BookingConstants.ScreeningSeatStatuses.Held
                         && effectiveBookingId.HasValue
                         && s.HeldByBookingId == effectiveBookingId.Value
            })
        };

        return Json(result);
    }

    // GET: /CounterTicket/Receipt/5
    // Hiển thị hóa đơn vé và hóa đơn bắp nước cùng giao dịch (nếu có)
    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var receipt = await counterTicketService.GetReceiptAsync(id);
        if (receipt == null)
        {
            return NotFound("Không tìm thấy hóa đơn bán vé tại quầy.");
        }

        ViewBag.ConcessionSale = receipt.ConcessionSale;
        return View(receipt.Booking);
    }
}
