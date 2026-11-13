using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Controllers;

/// <summary>
/// Quản lý bán bắp nước và theo dõi tồn kho tại quầy:
/// - UC-15: Record Snack & Drink Sale
/// - UC-16: Update Snack & Drink Stock
/// - UC-17: Low-Stock Alerts
/// Chuẩn 3-Layer MVC của PRN222: Controller mỏng, gọi qua IConcessionService.
/// </summary>
[SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
public class ConcessionController(IConcessionService concessionService) : Controller
{
    // GET: /Concession?page=1
    // Hiển thị danh mục bắp nước đang kinh doanh để bán hàng (UC-15, phân trang 10 món/trang)
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 10;
        if (page < 1) page = 1;

        var (items, totalCount) = await concessionService.GetActiveItemsPagedAsync(page, pageSize);
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var lowStockList = await concessionService.GetLowStockItemsAsync();
        ViewBag.LowStockCount = lowStockList.Count;

        string formToken = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("ConcessionFormToken", formToken);
        ViewBag.FormToken = formToken;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalCount;
        ViewBag.PageSize = pageSize;

        return View(items);
    }

    // GET: /Concession/LowStock?page=1
    // Cảnh báo các mặt hàng tồn kho thấp hoặc đã hết hàng (UC-17, phân trang 10 món/trang)
    [HttpGet]
    public async Task<IActionResult> LowStock(int page = 1)
    {
        const int pageSize = 10;
        if (page < 1) page = 1;

        var (items, totalCount, outOfStock, lowStock) = await concessionService.GetLowStockItemsPagedAsync(page, pageSize);
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalCount;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalOutOfStock = outOfStock;
        ViewBag.TotalLowStock = lowStock;

        return View(items);
    }

    // POST: /Concession/Sell
    // Thu ngân xác nhận bán các món bắp nước và trừ kho tự động (UC-15, UC-16)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sell(int[]? itemIds, int[]? quantities, string? paymentMethod = "Cash", string? formToken = null)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // 1. Chống double-submit qua FormToken
        if (!string.IsNullOrEmpty(formToken))
        {
            string? sessionToken = HttpContext.Session.GetString("ConcessionFormToken");
            if (string.IsNullOrEmpty(sessionToken) || sessionToken != formToken)
            {
                TempData["ErrorMessage"] = "Yêu cầu đã được xử lý hoặc biểu mẫu đã hết hạn. Vui lòng không gửi trùng lặp.";
                return RedirectToAction(nameof(Index));
            }
            HttpContext.Session.Remove("ConcessionFormToken");
        }

        // Kiểm tra tính hợp lệ của mảng dữ liệu gửi lên
        if (itemIds == null || quantities == null || itemIds.Length == 0 || itemIds.Length != quantities.Length)
        {
            TempData["ErrorMessage"] = "Dữ liệu gửi lên không hợp lệ hoặc bị lỗi cấu trúc.";
            return RedirectToAction(nameof(Index));
        }

        var items = new List<ConcessionSaleItemRequest>();
        for (int i = 0; i < itemIds.Length; i++)
        {
            int qty = quantities[i];
            // Chỉ đóng gói các món mà người dùng chọn bán (khác 0)
            if (qty != 0)
            {
                items.Add(new ConcessionSaleItemRequest(itemIds[i], qty));
            }
        }

        var request = new SellConcessionRequest(userId.Value, items, paymentMethod ?? "Cash");
        var result = await concessionService.SellAsync(request);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Có lỗi xảy ra khi thực hiện giao dịch.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Receipt), new { id = result.Data!.ConcessionSaleId });
    }

    // GET: /Concession/Receipt/5
    // Xem hóa đơn bắp nước đã bán thành công
    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var sale = await concessionService.GetSaleDetailsAsync(id);
        if (sale == null)
        {
            return NotFound("Không tìm thấy hóa đơn bắp nước.");
        }

        return View(sale);
    }
}
