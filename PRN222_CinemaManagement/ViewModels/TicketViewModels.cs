using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.ViewModels;

// Dùng cho màn hình nhân viên quầy tra cứu đơn vé (UC-18)
public class StaffLookupViewModel
{
    public string? SearchKeyword { get; set; }
    public List<Booking> Bookings { get; set; } = new();
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
