using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.Models.ViewModels;

// Dữ liệu hiển thị một phim trong danh sách phim đang chiếu.
public class MovieCardViewModel
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string AgeRating { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
}

// Thông tin một suất chiếu để khách hàng chọn giờ xem.
public class ShowtimeOptionViewModel
{
    public int ShowtimeId { get; set; }
    public string Auditorium { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public decimal BasePrice { get; set; }
}

public class MovieDetailsViewModel
{
    public MovieCardViewModel Movie { get; set; } = new MovieCardViewModel();
    public List<ShowtimeOptionViewModel> Showtimes { get; set; } = new List<ShowtimeOptionViewModel>();
}

// IsMine cho biết ghế có thuộc đơn đang giữ của khách hiện tại hay không.
public class SeatViewModel
{
    public int ShowtimeSeatId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string SeatType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsMine { get; set; }
}

public class SeatMapViewModel
{
    public int ShowtimeId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Auditorium { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public List<SeatViewModel> Seats { get; set; } = new List<SeatViewModel>();
    public int? BookingId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
}

public class BookingSeatViewModel
{
    public int ShowtimeSeatId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class TicketViewModel
{
    public int TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
}

// Dùng chung cho trang xác nhận, thanh toán và xem vé của chính khách hàng.
public class BookingDetailsViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int ShowtimeId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Auditorium { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
    public decimal TotalAmount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<BookingSeatViewModel> Seats { get; set; } = new List<BookingSeatViewModel>();
    public List<TicketViewModel> Tickets { get; set; } = new List<TicketViewModel>();
}

// Kết quả giữ ghế do server tính, gồm hạn giữ cố định và tổng tiền.
public class HoldResultViewModel
{
    public int? BookingId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
    public decimal TotalAmount { get; set; }
}

// Chỉ nhận ID ghế; không nhận giá tiền hoặc người mua từ trình duyệt.
public class SetSeatsRequest
{
    [Range(1, int.MaxValue)]
    public int ShowtimeId { get; set; }

    [Range(1, int.MaxValue)]
    public int? BookingId { get; set; }

    [Required, MaxLength(20)]
    public int[] ShowtimeSeatIds { get; set; } = Array.Empty<int>();
}

public class SimulatedPaymentRequest
{
    [Range(1, int.MaxValue)]
    public int BookingId { get; set; }

    [Required, RegularExpression("^(Success|Failed|Cancelled)$")]
    public string Outcome { get; set; } = string.Empty;

    [Required, RegularExpression("^(VNPay_Simulated|MoMo_Simulated)$")]
    public string Method { get; set; } = "VNPay_Simulated";
}

public static class BookingDisplay
{
    // Dữ liệu lưu theo UTC, giao diện hiển thị giờ Việt Nam (UTC+7).
    public static DateTime VietnamTime(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);
    }

    public static string StatusText(string status)
    {
        switch (status)
        {
            case "Pending": return "Chờ thanh toán";
            case "Confirmed": return "Đã thanh toán";
            case "Cancelled": return "Đã hủy";
            case "Expired": return "Hết hạn";
            case "Refunded": return "Đã hoàn tiền";
            default: return status;
        }
    }
}
