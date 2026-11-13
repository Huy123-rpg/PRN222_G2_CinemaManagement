using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Services.IService;

/// <summary>
/// Model kết quả thống kê doanh thu chung cho Daily (UC-28) và Shift (UC-29)
/// </summary>
public sealed record RevenueSummaryResult(
    List<Payment> Payments,
    List<ConcessionSale> ConcessionSales,
    decimal TicketRevenue,
    decimal TotalConcessionRevenue,
    decimal StandaloneConcessionRevenue,
    decimal BookingConcessionRevenue,
    decimal GrandTotalRevenue,
    decimal TotalRefund,
    int TicketOrderCount,
    int TotalTicketsSold,
    int ConcessionOrderCount,
    int StandaloneConcessionOrderCount);

/// <summary>
/// Model kết quả báo cáo hủy vé (UC-30)
/// </summary>
public sealed record CancellationReportResult(
    List<Booking> Bookings,
    decimal TotalCancelledValue,
    decimal TotalRefundObligation,
    decimal TotalRefund,
    decimal TotalRefundPending,
    int TotalCancelledTickets,
    int TotalBookings);

/// <summary>
/// Model chi tiết từng dòng đối soát (TEC-08)
/// </summary>
public class ReconciliationItem
{
    public string TransactionType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime TransactionTime { get; set; }
    public string Actor { get; set; } = string.Empty;
    public decimal HeaderAmount { get; set; }
    public decimal DetailAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } = "Matched"; // Matched, Discrepancy, InsufficientData
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Model kết quả tổng hợp đối soát (TEC-08)
/// </summary>
public sealed record ReconciliationResult(
    List<ReconciliationItem> Items,
    decimal GrossRevenue,
    decimal TotalRefund,
    decimal NetRevenue,
    decimal TotalTicketRevenue,
    decimal TotalConcessionRevenue,
    decimal StandaloneRevenue,
    decimal BookingConcessionRevenue,
    int TotalChecked,
    int DiscrepancyCount,
    int InsufficientCount,
    string OverallStatus);

/// <summary>
/// Hợp đồng dịch vụ báo cáo doanh thu và đối soát.
/// Chuẩn Lab 2 PRN222: Interface rõ ràng, tập trung đúng 4 use case.
/// </summary>
public interface IRevenueReportService
{
    Task<RevenueSummaryResult> CalculateRevenueSummaryAsync(DateTime utcStart, DateTime utcEnd, int? staffId = null);
    Task<List<Staff>> GetStaffListAsync();
    Task<CancellationReportResult> GetCancellationReportAsync(DateTime? fromDate, DateTime? toDate, string? statusFilter);
    Task<ReconciliationResult> GetReconciliationAsync(DateTime utcStart, DateTime utcEnd);
}
