using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;

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
    // GET: /CounterTicket
    // Hiển thị danh sách phim đang chiếu và các suất chiếu có thể bán vé.
    [HttpGet]
    public async Task<IActionResult> Index(int? movieId)
    {
        ViewBag.Movies = await counterTicketService.GetShowingMoviesAsync();
        ViewBag.SelectedMovieId = movieId;

        var screenings = await counterTicketService.GetAvailableScreeningsAsync(movieId);
        return View(screenings);
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

        ViewBag.Screening = screening;
        ViewBag.ConcessionItems = await counterTicketService.GetAvailableConcessionsAsync();
        return View(seats);
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
        string? paymentMethod = "Cash")
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
            paymentMethod ?? "Cash");

        var result = await counterTicketService.SellTicketsAsync(request);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Có lỗi xảy ra trong quá trình bán vé.";
            return RedirectToAction(nameof(SelectSeats), new { screeningId });
        }

        TempData["SuccessMessage"] = result.Message;

        // Chỉ cần truyền BookingId, Service/Repository tự động tìm đúng hóa đơn bắp nước cùng giao dịch
        return RedirectToAction(nameof(Receipt), new { id = result.Data!.Booking.BookingId });
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
