using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.ViewModels;

// Dùng cho màn hình nhân viên quầy tra cứu đơn vé (UC-18)
public class StaffLookupViewModel
{
    public string? SearchKeyword { get; set; }
    public List<Booking> Bookings { get; set; } = new();
}

// Dùng cho màn hình nhân viên quầy xử lý hoàn tiền mặt (UC-14/UC-30)
public class StaffRefundItemViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public DateTime BookingDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string AuditoriumName { get; set; } = string.Empty;
    public DateTime ScreeningStartAt { get; set; }
    public List<string> SeatLabels { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RefundPaid { get; set; }
    public decimal RefundDue { get; set; }
    public string Status { get; set; } = string.Empty; // "Cancelled" or "Refunded"
    public string BookingChannel { get; set; } = "Online";
}

public class StaffRefundQueueViewModel
{
    public string? SearchKeyword { get; set; }
    public string? StatusFilter { get; set; } = "Pending"; // "Pending", "Completed", "All"
    public decimal TotalPendingAmount { get; set; }
    public decimal TotalRefundedAmount { get; set; }
    public int PendingCount { get; set; }
    public int CompletedCount { get; set; }
    public List<StaffRefundItemViewModel> Items { get; set; } = new();
}

// Dùng cho việc kiểm tra quy tắc hủy vé (TEC-06)
public static class CancellationRules
{
    // Quy tắc TEC-06: Chỉ được hủy trước giờ chiếu ít nhất 60 phút và vé chưa từng check-in
    public static (bool IsEligible, string Reason) CheckEligibility(Booking booking)
    {
        if (booking.Screening == null)
        {
            return (false, "Không tìm thấy thông tin suất chiếu.");
        }

        return CheckEligibility(booking.Status, booking.Screening.StartAt, booking.Tickets.Select(t => t.Status));
    }

    public static (bool IsEligible, string Reason) CheckEligibility(string status, DateTime startAt, IEnumerable<string> ticketStatuses)
    {
        if (status != "Confirmed")
        {
            return (false, $"Đơn vé đang ở trạng thái '{status}', không thể hủy.");
        }

        if (ticketStatuses.Any(s => s == "CheckedIn"))
        {
            return (false, "Vé trong đơn này đã được soát vào rạp (CheckedIn), không thể hủy.");
        }

        var timeUntilShow = startAt - DateTime.UtcNow;
        if (timeUntilShow.TotalMinutes < 60)
        {
            return (false, "Quy định chỉ được phép hủy trước giờ chiếu ít nhất 60 phút.");
        }

        return (true, "Đủ điều kiện hủy vé.");
    }
}
