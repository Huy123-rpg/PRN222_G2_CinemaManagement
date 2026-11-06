namespace PRN222_CinemaManagement.Services.OnlineBooking;

/// <summary>
/// Các hằng số trạng thái nghiệp vụ. Giá trị bắt buộc khớp với CHECK constraint
/// khai báo trong <c>Data/CinemaDbContext.cs</c> để không sinh lỗi SQL khi ghi.
/// </summary>
public static class BookingStates
{
    // Trạng thái ghế trong một suất chiếu (bảng ShowtimeSeats).
    public const string SeatAvailable = "Available"; // Ghế trống, ai cũng chọn được.
    public const string SeatHeld = "Held";           // Ghế đang được một đơn giữ tạm (tối đa 5 phút).
    public const string SeatSold = "Sold";           // Ghế đã bán, không thể chọn lại.
    public const string SeatBlocked = "Blocked";     // Ghế không được mở bán.

    // Trạng thái đơn đặt vé (bảng Bookings).
    public const string BookingPending = "Pending";     // Đang giữ ghế, chờ thanh toán.
    public const string BookingConfirmed = "Confirmed"; // Đã thanh toán, đã phát vé.
    public const string BookingCancelled = "Cancelled"; // Khách hủy, ghế đã được giải phóng.
    public const string BookingExpired = "Expired";     // Hết hạn giữ ghế, ghế đã được giải phóng.

    // Trạng thái thanh toán (bảng Payments).
    public const string PaymentSuccess = "Success";
    public const string PaymentFailed = "Failed";
    public const string PaymentCancelled = "Cancelled";

    /// <summary>Thời gian tối đa được giữ ghế (phút). Hạn cố định do server quyết định.</summary>
    public const int HoldMinutes = 5;

    /// <summary>Số ghế tối đa cho một đơn.</summary>
    public const int MaxSeatsPerBooking = 20;
}
