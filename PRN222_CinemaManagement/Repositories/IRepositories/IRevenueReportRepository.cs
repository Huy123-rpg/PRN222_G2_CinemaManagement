using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

/// <summary>
/// Data access contract for Revenue Reports (UC-28, UC-29, UC-30, TEC-08).
/// Chuẩn Lab 2 PRN222: Chỉ chứa các hàm truy vấn EF Core cần thiết.
/// </summary>
public interface IRevenueReportRepository
{
    Task<List<Payment>> GetPaymentsAsync(DateTime utcStart, DateTime utcEnd, int? staffId = null);
    Task<List<ConcessionSale>> GetConcessionSalesAsync(DateTime utcStart, DateTime utcEnd, int? staffId = null);
    Task<List<Staff>> GetStaffListAsync();
    Task<List<Booking>> GetCancelledBookingsAsync(DateTime? utcFrom, DateTime? utcTo, string? statusFilter);
    Task<List<Booking>> GetBookingsForReconciliationAsync(DateTime utcStart, DateTime utcEnd);
}
