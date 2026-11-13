using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.ImpleRepositories;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.ImpleService;
using PRN222_CinemaManagement.Services.IService;

namespace IntegrationSmokeTest;

public static class Program
{
    private const string TargetDb = "CinemaDB_Dev";

    private static string GetSafeConnectionString()
    {
        string conn = "Server=.;Database=CinemaDB_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        var builder = new SqlConnectionStringBuilder(conn);

        if (!string.Equals(builder.InitialCatalog.Trim(), TargetDb, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"[AN TOÀN CSDL] Database '{builder.InitialCatalog}' không được phép! Chỉ cho phép '{TargetDb}'.");
        }

        return conn;
    }

    public static async Task<int> Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("===============================================================");
        Console.WriteLine("   BÀI KIỂM THỬ TÍCH HỢP TOÀN DIỆN 9 NHIỆM VỤ PRN222");
        Console.WriteLine($"   Target Database: {TargetDb}");
        Console.WriteLine("===============================================================\n");

        string conn = GetSafeConnectionString();

        var services = new ServiceCollection();
        services.AddDbContext<CinemaDbContext>(options => options.UseSqlServer(conn));
        services.AddScoped<IConcessionRepository, ConcessionRepository>();
        services.AddScoped<IConcessionService, ConcessionService>();
        services.AddScoped<ICounterTicketRepository, CounterTicketRepository>();
        services.AddScoped<ICounterTicketService, CounterTicketService>();
        services.AddScoped<IRevenueReportRepository, RevenueReportRepository>();
        services.AddScoped<IRevenueReportService, RevenueReportService>();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var concessionService = scope.ServiceProvider.GetRequiredService<IConcessionService>();
        var counterService = scope.ServiceProvider.GetRequiredService<ICounterTicketService>();
        var revenueService = scope.ServiceProvider.GetRequiredService<IRevenueReportService>();

        // 1. Kiểm tra tài khoản Staff và chuẩn bị
        var staffUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "staff");
        if (staffUser == null)
        {
            Console.WriteLine("[LỖI] Không tìm thấy user 'staff' trong CinemaDB_Dev!");
            return 1;
        }

        int passed = 0;
        int failed = 0;

        void Assert(string testName, bool condition, string detail = "")
        {
            if (condition)
            {
                Console.WriteLine($"  [PASS] {testName} {(string.IsNullOrEmpty(detail) ? "" : "-> " + detail)}");
                passed++;
            }
            else
            {
                Console.WriteLine($"  [FAIL] {testName} {(string.IsNullOrEmpty(detail) ? "" : "-> " + detail)}");
                failed++;
            }
        }

        // -------------------------------------------------------------
        // PHẦN A: KIỂM THỬ UC-15, UC-16, UC-17 (CONCESSION)
        // -------------------------------------------------------------
        Console.WriteLine("--- 1. KIỂM THỬ UC-15, UC-16, UC-17: CONCESSION SALES & STOCK ---");

        var activeItems = await concessionService.GetActiveItemsAsync();
        Assert("UC-15: Lấy danh mục bắp nước Active", activeItems.Count >= 3, $"Tìm thấy {activeItems.Count} món");

        var testItem = activeItems[0];
        int originalStock = testItem.StockQuantity;

        // Bán 1 phần testItem bằng Tiền mặt
        var sellReq = new SellConcessionRequest(
            staffUser.UserId,
            new List<ConcessionSaleItemRequest> { new(testItem.ConcessionItemId, 1) },
            "Cash");

        var sellResult = await concessionService.SellAsync(sellReq);
        Assert("UC-15: Bán bắp nước tại quầy (Thành công)", sellResult.Succeeded, $"Mã đơn: {sellResult.Data?.SaleCode}, Tổng: {sellResult.Data?.TotalAmount:N0}đ");

        // Kiểm tra UC-16: Tồn kho giảm đúng 1
        var itemAfterSell = await db.ConcessionItems.AsNoTracking().FirstOrDefaultAsync(c => c.ConcessionItemId == testItem.ConcessionItemId);
        Assert("UC-16: Trừ kho thành công sau giao dịch", itemAfterSell?.StockQuantity == originalStock - 1, $"Kho cũ: {originalStock} -> Kho mới: {itemAfterSell?.StockQuantity}");

        // Kiểm tra UC-16: Validation không cho số lượng <= 0
        var invalidQtyReq = new SellConcessionRequest(
            staffUser.UserId,
            new List<ConcessionSaleItemRequest> { new(testItem.ConcessionItemId, 0) },
            "Cash");
        var invalidQtyRes = await concessionService.SellAsync(invalidQtyReq);
        Assert("UC-16: Từ chối số lượng <= 0", !invalidQtyRes.Succeeded, invalidQtyRes.Errors.FirstOrDefault()?.Message ?? "");

        // Kiểm tra UC-16: Validation không cho bán vượt tồn kho
        var overStockReq = new SellConcessionRequest(
            staffUser.UserId,
            new List<ConcessionSaleItemRequest> { new(testItem.ConcessionItemId, 99999) },
            "Cash");
        var overStockRes = await concessionService.SellAsync(overStockReq);
        Assert("UC-16: Từ chối bán vượt tồn kho (Stock không bị âm)", !overStockRes.Succeeded, overStockRes.Errors.FirstOrDefault()?.Message ?? "");

        // Kiểm tra UC-17: Low stock alert
        var lowStockList = await concessionService.GetLowStockItemsAsync();
        Assert("UC-17: Truy vấn cảnh báo tồn kho thấp", lowStockList != null, $"Số món dưới ngưỡng: {lowStockList?.Count ?? 0}");

        // -------------------------------------------------------------
        // PHẦN B: KIỂM THỬ UC-13 (COUNTER TICKET SALES KÈM COMBO)
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 2. KIỂM THỬ UC-13: COUNTER TICKET SALES ---");

        var movies = await counterService.GetShowingMoviesAsync();
        Assert("UC-13: Lấy danh sách phim đang chiếu", movies.Count > 0, $"Có {movies.Count} phim");

        var screenings = await counterService.GetAvailableScreeningsAsync(null);
        Assert("UC-13: Lấy danh sách suất chiếu khả dụng", screenings.Count > 0, $"Có {screenings.Count} suất chiếu");

        var chosenScreening = screenings.First();
        var (scr, seats) = await counterService.GetScreeningSeatsMapAsync(chosenScreening.ScreeningId);
        var availableSeats = seats.Where(s => s.Status == "Available").ToList();
        Assert("UC-13: Lấy sơ đồ ghế phòng chiếu", availableSeats.Count > 0, $"Phòng {scr?.Auditorium?.Name}: còn {availableSeats.Count} ghế trống");

        var seatToBuy = availableSeats.First();
        int concItemStockBefore = itemAfterSell?.StockQuantity ?? 0;

        // Bán 1 vé kèm 1 bắp nước
        var counterSellReq = new SellCounterTicketsRequest(
            staffUser.UserId,
            chosenScreening.ScreeningId,
            new List<int> { seatToBuy.ScreeningSeatId },
            new List<CounterConcessionOrder> { new(testItem.ConcessionItemId, 1) },
            "Cash");

        var counterResult = await counterService.SellTicketsAsync(counterSellReq);
        Assert("UC-13: Bán vé tại quầy kèm bắp nước (Giao dịch thành công)", counterResult.Succeeded, $"BookingCode: {counterResult.Data?.Booking?.BookingCode}");

        // Kiểm tra ghế đã chuyển sang Sold
        var seatAfter = await db.ScreeningSeats.AsNoTracking().FirstOrDefaultAsync(ss => ss.ScreeningSeatId == seatToBuy.ScreeningSeatId);
        Assert("UC-13: Ghế đã chuyển sang trạng thái 'Sold'", seatAfter?.Status == "Sold");

        // Kiểm tra tồn kho bắp nước mua kèm đã giảm tiếp 1
        var itemAfterCounter = await db.ConcessionItems.AsNoTracking().FirstOrDefaultAsync(c => c.ConcessionItemId == testItem.ConcessionItemId);
        Assert("UC-13: Tồn kho bắp nước mua kèm được trừ chính xác", itemAfterCounter?.StockQuantity == concItemStockBefore - 1, $"Kho: {concItemStockBefore} -> {itemAfterCounter?.StockQuantity}");

        // Kiểm tra Hóa đơn và Mã liên kết
        int bookingId = counterResult.Data!.Booking!.BookingId;
        var receipt = await counterService.GetReceiptAsync(bookingId);
        Assert("UC-13: Hóa đơn vé được tạo với BookingChannel='Counter'", receipt?.Booking?.BookingChannel == "Counter");
        Assert("UC-13: Hóa đơn bắp nước liên kết đúng mã CS-{BookingCode}",
            receipt?.ConcessionSale != null && receipt.ConcessionSale.SaleCode == $"CS-{receipt.Booking.BookingCode}",
            $"Linked SaleCode: {receipt?.ConcessionSale?.SaleCode}");

        // Kiểm tra chống bán trùng ghế (Double-sell)
        var doubleSellReq = new SellCounterTicketsRequest(
            staffUser.UserId,
            chosenScreening.ScreeningId,
            new List<int> { seatToBuy.ScreeningSeatId },
            null,
            "Cash");
        var doubleSellRes = await counterService.SellTicketsAsync(doubleSellReq);
        Assert("UC-13: Chống bán trùng ghế (Ghế đã Sold bị từ chối & Rollback)", !doubleSellRes.Succeeded, doubleSellRes.Errors.FirstOrDefault()?.Message ?? "");

        // -------------------------------------------------------------
        // PHẦN C: KIỂM THỬ UC-28, UC-29, UC-30, TEC-08 (REVENUE REPORTS)
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 3. KIỂM THỬ UC-28, UC-29, UC-30, TEC-08: DOANH THU & ĐỐI SOÁT ---");

        var today = DateTime.UtcNow.AddHours(7).Date;
        var utcStart = today.AddHours(-7);
        var utcEnd = utcStart.AddDays(1);

        // UC-28: Daily Revenue
        var dailySummary = await revenueService.CalculateRevenueSummaryAsync(utcStart, utcEnd);
        Assert("UC-28: Tính doanh thu vé trong ngày", dailySummary.TicketRevenue > 0, $"Doanh thu vé: {dailySummary.TicketRevenue:N0}đ");
        Assert("UC-28: Tính doanh thu bắp nước trong ngày", dailySummary.TotalConcessionRevenue > 0, $"Doanh thu bắp nước: {dailySummary.TotalConcessionRevenue:N0}đ");
        Assert("UC-28: Tổng doanh thu = Doanh thu vé + Bắp nước",
            dailySummary.GrandTotalRevenue == dailySummary.TicketRevenue + dailySummary.TotalConcessionRevenue,
            $"Tổng: {dailySummary.GrandTotalRevenue:N0}đ");

        // UC-29: Shift Revenue
        var staffList = await revenueService.GetStaffListAsync();
        var shiftSummary = await revenueService.CalculateRevenueSummaryAsync(utcStart, utcEnd, staffList.First().StaffId);
        Assert("UC-29: Báo cáo doanh thu theo ca & nhân viên thu ngân", shiftSummary != null, $"Doanh thu ca nhân viên: {shiftSummary?.GrandTotalRevenue:N0}đ");

        // UC-30: Cancellation Report
        var cancelReport = await revenueService.GetCancellationReportAsync(today, today, "All");
        Assert("UC-30: Báo cáo hủy vé và hoàn tiền", cancelReport != null, $"Số đơn hủy: {cancelReport?.TotalBookings ?? 0}, Tiền hoàn: {cancelReport?.TotalRefund:N0}đ");

        // TEC-08: Revenue Reconciliation
        var reconciliation = await revenueService.GetReconciliationAsync(utcStart, utcEnd);
        Assert("TEC-08: Đối soát doanh thu 3-chiều", reconciliation.TotalChecked > 0, $"Tổng giao dịch kiểm tra: {reconciliation.TotalChecked}");
        Assert("TEC-08: Giao dịch quầy khớp hoàn toàn (Matched)",
            reconciliation.DiscrepancyCount == 0,
            $"Số lệch: {reconciliation.DiscrepancyCount}, Số chưa đủ dữ liệu: {reconciliation.InsufficientCount}, Trạng thái: {reconciliation.OverallStatus}");

        Console.WriteLine("\n===============================================================");
        Console.WriteLine($"   KẾT QUẢ KIỂM THỬ: {passed} PASSED | {failed} FAILED");
        Console.WriteLine("===============================================================");

        return failed == 0 ? 0 : 1;
    }
}
