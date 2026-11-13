using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

/// <summary>
/// EF Core implementation of <see cref="IRevenueReportRepository"/>.
/// Chuẩn Lab 2 PRN222: Truy vấn LINQ trực tiếp, AsNoTracking() tối ưu đọc báo cáo.
/// </summary>
public class RevenueReportRepository(CinemaDbContext context) : IRevenueReportRepository
{
    public Task<List<Payment>> GetPaymentsAsync(DateTime utcStart, DateTime utcEnd, int? staffId = null)
    {
        var query = context.Payments.AsNoTracking()
            .Include(p => p.Booking).ThenInclude(b => b.Tickets)
            .Include(p => p.Booking).ThenInclude(b => b.BookingConcessions)
            .Include(p => p.Booking).ThenInclude(b => b.Staff).ThenInclude(s => s!.User)
            .Include(p => p.Booking).ThenInclude(b => b.Customer).ThenInclude(c => c!.User)
            .Include(p => p.Booking).ThenInclude(b => b.Screening).ThenInclude(sc => sc.Movie)
            .Where(p => p.PaymentStatus == "Success"
                     && p.PaidAt.HasValue
                     && p.PaidAt.Value >= utcStart
                     && p.PaidAt.Value < utcEnd
                     && p.Amount > 0);

        if (staffId.HasValue && staffId.Value > 0)
        {
            query = query.Where(p => p.Booking.StaffId == staffId.Value);
        }

        return query.OrderByDescending(p => p.PaidAt).ToListAsync();
    }

    public Task<List<ConcessionSale>> GetConcessionSalesAsync(DateTime utcStart, DateTime utcEnd, int? staffId = null)
    {
        var query = context.ConcessionSales.AsNoTracking()
            .Include(cs => cs.Staff).ThenInclude(s => s!.User)
            .Include(cs => cs.ConcessionSaleItems).ThenInclude(csi => csi.ConcessionItem)
            .Where(cs => cs.Status == "Completed"
                     && cs.SaleDate >= utcStart
                     && cs.SaleDate < utcEnd
                     && cs.TotalAmount >= 0);

        if (staffId.HasValue && staffId.Value > 0)
        {
            query = query.Where(cs => cs.StaffId == staffId.Value);
        }

        return query.OrderByDescending(cs => cs.SaleDate).ToListAsync();
    }

    public Task<List<Staff>> GetStaffListAsync() =>
        context.Staffs.AsNoTracking()
            .Include(s => s.User)
            .OrderBy(s => s.StaffCode)
            .ToListAsync();

    public Task<List<Booking>> GetCancelledBookingsAsync(DateTime? utcFrom, DateTime? utcTo, string? statusFilter)
    {
        var query = context.Bookings.AsNoTracking()
            .Include(b => b.Tickets).ThenInclude(t => t.ScreeningSeat).ThenInclude(ss => ss.Seat)
            .Include(b => b.Payments)
            .Include(b => b.Screening).ThenInclude(sc => sc.Movie)
            .Include(b => b.Screening).ThenInclude(sc => sc.Auditorium)
            .Include(b => b.Customer).ThenInclude(c => c!.User)
            .Include(b => b.Staff).ThenInclude(s => s!.User)
            .Where(b => (b.Status == "Cancelled"
                      || b.Status == "Refunded"
                      || b.Tickets.Any(t => t.Status == "Cancelled")
                      || b.Payments.Any(p => p.RefundAmount > 0))
                     && (b.Tickets.Any() || b.Payments.Any(p => p.PaymentStatus == "Success" || p.RefundAmount > 0)));

        if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
        {
            query = query.Where(b => b.Status == statusFilter);
        }

        if (utcFrom.HasValue)
        {
            query = query.Where(b => b.BookingDate >= utcFrom.Value);
        }

        if (utcTo.HasValue)
        {
            query = query.Where(b => b.BookingDate < utcTo.Value);
        }

        return query.OrderByDescending(b => b.BookingDate).ToListAsync();
    }

    public Task<List<Booking>> GetBookingsForReconciliationAsync(DateTime utcStart, DateTime utcEnd) =>
        context.Bookings.AsNoTracking()
            .Include(b => b.Payments)
            .Include(b => b.Tickets)
            .Include(b => b.BookingConcessions)
            .Include(b => b.Staff).ThenInclude(s => s!.User)
            .Include(b => b.Customer).ThenInclude(c => c!.User)
            .Include(b => b.Screening).ThenInclude(sc => sc.Movie)
            .Where(b => ((b.BookingDate >= utcStart && b.BookingDate < utcEnd)
                      || b.Payments.Any(p => p.PaidAt.HasValue && p.PaidAt.Value >= utcStart && p.PaidAt.Value < utcEnd))
                     && b.Status != "Pending"
                     && b.Status != "Expired"
                     && !(b.Status == "Cancelled" && !b.Payments.Any(p => p.PaymentStatus == "Success" || p.RefundAmount > 0)))
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();
}
