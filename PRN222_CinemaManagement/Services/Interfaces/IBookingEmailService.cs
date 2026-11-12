using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Services.Interfaces;

/// <summary>Gửi email xác nhận đặt vé kèm mã QR vé xem phim và mã QR bắp nước cho khách hàng.</summary>
public interface IBookingEmailService
{
    Task SendBookingConfirmationAsync(BookingDetailsViewModel booking, CancellationToken ct = default);
}

