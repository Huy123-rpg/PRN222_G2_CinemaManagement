using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.Models.ViewModels;

/// <summary>Dữ liệu công khai của phim; không trả entity hoặc thông tin khách hàng ra trình duyệt.</summary>
public record MovieCardViewModel(int MovieId, string Title, string? Description, int DurationMinutes,
    string AgeRating, string? PosterUrl);
public record ShowtimeOptionViewModel(int ShowtimeId, string Auditorium, DateTime StartAt, decimal BasePrice);
public record MovieDetailsViewModel(MovieCardViewModel Movie, IReadOnlyList<ShowtimeOptionViewModel> Showtimes);
public record SeatViewModel(int ShowtimeSeatId, string RowLabel, int SeatNumber, string SeatType,
    decimal Price, string Status, bool IsMine);
public record SeatMapViewModel(int ShowtimeId, string MovieTitle, string Auditorium, DateTime StartAt,
    IReadOnlyList<SeatViewModel> Seats, int? BookingId, DateTime? ExpiresAt, DateTime ServerNow);
public record BookingSeatViewModel(int ShowtimeSeatId, string Label, string SeatType, decimal Price);
public record TicketViewModel(int TicketId, string TicketCode, string SeatLabel, decimal Price, string Status);
public record BookingDetailsViewModel(int BookingId, string BookingCode, int ShowtimeId, string MovieTitle,
    string Auditorium, DateTime StartAt, string Status, DateTime ExpiresAt, DateTime ServerNow,
    decimal TotalAmount, string CustomerName, string Email, IReadOnlyList<BookingSeatViewModel> Seats,
    IReadOnlyList<TicketViewModel> Tickets);
public record HoldResultViewModel(int? BookingId, DateTime? ExpiresAt, DateTime ServerNow, decimal TotalAmount);

/// <summary>Chỉ nhận ID ghế; giá, người mua và hạn giữ luôn được lấy/tính tại server.</summary>
public class SetSeatsRequest
{
    [Range(1, int.MaxValue)]
    public int ShowtimeId { get; set; }
    [Range(1, int.MaxValue)]
    public int? BookingId { get; set; }
    [Required, MaxLength(20)]
    public int[] ShowtimeSeatIds { get; set; } = [];
}

public class SimulatedPaymentRequest
{
    [Range(1, int.MaxValue)]
    public int BookingId { get; set; }
    [Required, RegularExpression("^(Success|Failed|Cancelled)$")]
    public string Outcome { get; set; } = "";
    [Required, RegularExpression("^(VNPay_Simulated|MoMo_Simulated)$")]
    public string Method { get; set; } = "VNPay_Simulated";
}

/// <summary>SQL lưu UTC; hiển thị cố định giờ Việt Nam, không phụ thuộc múi giờ máy chạy.</summary>
public static class BookingDisplay
{
    public static DateTime VietnamTime(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);
    public static string StatusText(string status) => status switch
    {
        "Pending" => "Chờ thanh toán", "Confirmed" => "Đã thanh toán", "Cancelled" => "Đã hủy",
        "Expired" => "Hết hạn", "Refunded" => "Đã hoàn tiền", _ => status
    };
}
