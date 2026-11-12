using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Controllers;

/// <summary>
/// Quản lý báo cáo doanh thu và đối soát:
/// - UC-28: Daily Revenue Report (Báo cáo doanh thu theo ngày)
/// - UC-29: Shift Revenue Report (Báo cáo doanh thu theo ca)
/// - UC-30: Cancellation Report (Báo cáo hủy vé)
/// - TEC-08: Revenue Reconciliation (Đối soát doanh thu)
/// Chuẩn 3-Layer MVC & Lab 2 PRN222: Controller mỏng, tiếp nhận tham số, gọi qua Service.
/// Phân quyền: Chỉ Quản lý (Admin, CinemaManager) được xem doanh thu toàn rạp.
/// </summary>
[SessionAuthorize(Roles = "Admin,CinemaManager")]
public class RevenueReportController(IRevenueReportService revenueReportService) : Controller
{
    // GET: /RevenueReport/Daily?date=2026-11-10 (UC-28)
    [HttpGet]
    public async Task<IActionResult> Daily(DateTime? date)
    {
        var vnDate = date?.Date ?? DateTime.UtcNow.AddHours(7).Date;
        var utcStart = vnDate.AddHours(-7);
        var utcEnd = utcStart.AddDays(1);

        ViewBag.SelectedDate = vnDate.ToString("yyyy-MM-dd");
        ViewBag.DisplayDate = vnDate.ToString("dd/MM/yyyy");

        var summary = await revenueReportService.CalculateRevenueSummaryAsync(utcStart, utcEnd);

        ViewBag.TotalTicketRevenue = summary.TicketRevenue;
        ViewBag.TotalConcessionRevenue = summary.TotalConcessionRevenue;
        ViewBag.StandaloneConcessionRevenue = summary.StandaloneConcessionRevenue;
        ViewBag.BookingConcessionRevenue = summary.BookingConcessionRevenue;
        ViewBag.GrandTotalRevenue = summary.GrandTotalRevenue;
        ViewBag.TotalRefund = summary.TotalRefund;
        ViewBag.TicketOrderCount = summary.TicketOrderCount;
        ViewBag.TotalTicketsSold = summary.TotalTicketsSold;
        ViewBag.ConcessionOrderCount = summary.ConcessionOrderCount;
        ViewBag.StandaloneConcessionOrderCount = summary.StandaloneConcessionOrderCount;
        ViewBag.ConcessionSales = summary.ConcessionSales;

        return View(summary.Payments);
    }

    // GET: /RevenueReport/Shift?date=2026-11-10&startTime=08:00&endTime=16:00&staffId=1 (UC-29)
    [HttpGet]
    public async Task<IActionResult> Shift(DateTime? date, string? startTime, string? endTime, int? staffId)
    {
        var vnDate = date?.Date ?? DateTime.UtcNow.AddHours(7).Date;
        if (!TimeSpan.TryParse(startTime, out var startTs)) startTs = new TimeSpan(8, 0, 0);
        if (!TimeSpan.TryParse(endTime, out var endTs)) endTs = new TimeSpan(16, 0, 0);

        var vnStart = vnDate.Add(startTs);
        DateTime vnEnd;
        bool isEndDay = (endTs.Hours == 23 && endTs.Minutes == 59);

        if (isEndDay)
        {
            // Nếu chọn kết thúc 23:59 để xem hết ngày: mốc kết thúc không bao gồm là 00:00 ngày tiếp theo
            vnEnd = endTs <= startTs ? vnDate.AddDays(2) : vnDate.AddDays(1);
        }
        else
        {
            vnEnd = endTs <= startTs ? vnDate.AddDays(1).Add(endTs) : vnDate.Add(endTs);
        }

        var utcStart = vnStart.AddHours(-7);
        var utcEnd = vnEnd.AddHours(-7);

        ViewBag.SelectedDate = vnDate.ToString("yyyy-MM-dd");
        ViewBag.StartTime = startTs.ToString(@"hh\:mm");
        ViewBag.EndTime = endTs.ToString(@"hh\:mm");
        ViewBag.VnStartDisplay = vnStart.ToString("HH:mm dd/MM/yyyy");
        // Hiển thị giờ kết thúc nhất quán với dữ liệu nhập (23:59 thay vì hiển thị 00:00 gây hiểu lầm)
        ViewBag.VnEndDisplay = isEndDay
            ? (endTs <= startTs ? vnDate.AddDays(1).ToString("23:59 dd/MM/yyyy") : vnDate.ToString("23:59 dd/MM/yyyy"))
            : vnEnd.ToString("HH:mm dd/MM/yyyy");
        ViewBag.IsOvernight = endTs <= startTs;
        ViewBag.SelectedStaffId = staffId;
        ViewBag.StaffList = await revenueReportService.GetStaffListAsync();

        var summary = await revenueReportService.CalculateRevenueSummaryAsync(utcStart, utcEnd, staffId);

        ViewBag.TotalTicketRevenue = summary.TicketRevenue;
        ViewBag.TotalConcessionRevenue = summary.TotalConcessionRevenue;
        ViewBag.StandaloneConcessionRevenue = summary.StandaloneConcessionRevenue;
        ViewBag.BookingConcessionRevenue = summary.BookingConcessionRevenue;
        ViewBag.GrandTotalRevenue = summary.GrandTotalRevenue;
        ViewBag.TotalRefund = summary.TotalRefund;
        ViewBag.TicketOrderCount = summary.TicketOrderCount;
        ViewBag.TotalTicketsSold = summary.TotalTicketsSold;
        ViewBag.ConcessionOrderCount = summary.ConcessionOrderCount;
        ViewBag.StandaloneConcessionOrderCount = summary.StandaloneConcessionOrderCount;
        ViewBag.ConcessionSales = summary.ConcessionSales;

        return View(summary.Payments);
    }

    // GET: /RevenueReport/Cancellation?fromDate=...&toDate=...&statusFilter=... (UC-30)
    [HttpGet]
    public async Task<IActionResult> Cancellation(DateTime? fromDate, DateTime? toDate, string? statusFilter)
    {
        var result = await revenueReportService.GetCancellationReportAsync(fromDate, toDate, statusFilter);

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd") ?? "";
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd") ?? "";
        ViewBag.StatusFilter = statusFilter ?? "All";
        ViewBag.TotalBookings = result.TotalBookings;
        ViewBag.TotalCancelledTickets = result.TotalCancelledTickets;
        ViewBag.TotalCancelledValue = result.TotalCancelledValue;
        ViewBag.TotalRefunded = result.TotalRefund;

        return View(result.Bookings);
    }

    // GET: /RevenueReport/Reconciliation?date=2026-11-10 (TEC-08)
    [HttpGet]
    public async Task<IActionResult> Reconciliation(DateTime? date)
    {
        var vnDate = date?.Date ?? DateTime.UtcNow.AddHours(7).Date;
        var utcStart = vnDate.AddHours(-7);
        var utcEnd = utcStart.AddDays(1);

        ViewBag.SelectedDate = vnDate.ToString("yyyy-MM-dd");
        ViewBag.DisplayDate = vnDate.ToString("dd/MM/yyyy");

        var rec = await revenueReportService.GetReconciliationAsync(utcStart, utcEnd);

        ViewBag.GrossRevenue = rec.GrossRevenue;
        ViewBag.TotalRefund = rec.TotalRefund;
        ViewBag.NetRevenue = rec.NetRevenue;
        ViewBag.TotalTicketRevenue = rec.TotalTicketRevenue;
        ViewBag.TotalConcessionRevenue = rec.TotalConcessionRevenue;
        ViewBag.StandaloneRevenue = rec.StandaloneRevenue;
        ViewBag.BookingConcessionRevenue = rec.BookingConcessionRevenue;
        ViewBag.TotalChecked = rec.TotalChecked;
        ViewBag.DiscrepancyCount = rec.DiscrepancyCount;
        ViewBag.InsufficientCount = rec.InsufficientCount;
        ViewBag.OverallStatus = rec.OverallStatus;

        return View(rec.Items);
    }
}
