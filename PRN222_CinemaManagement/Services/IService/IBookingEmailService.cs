using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

/// <summary>Gửi email xác nhận đặt vé kèm mã QR vé xem phim và mã QR bắp nước cho khách hàng.</summary>
public interface IBookingEmailService
{
    Task SendBookingConfirmationAsync(BookingDetailsViewModel booking, CancellationToken ct = default);
}

