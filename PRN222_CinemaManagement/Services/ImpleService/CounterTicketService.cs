using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>
/// Implementation of <see cref="ICounterTicketService"/>.
/// Chuẩn Lab 2 PRN222: Code phẳng, kiểm tra điều kiện bằng if/else tuần tự, dễ giải thích khi review.
/// </summary>
public class CounterTicketService(ICounterTicketRepository counterTicketRepository) : ICounterTicketService
{
    public Task<List<Movie>> GetShowingMoviesAsync() =>
        counterTicketRepository.GetShowingMoviesAsync();

    public Task<List<Screening>> GetAvailableScreeningsAsync(int? movieId) =>
        counterTicketRepository.GetAvailableScreeningsAsync(movieId);

    public async Task<(Screening? Screening, List<ScreeningSeat> Seats)> GetScreeningSeatsMapAsync(int screeningId)
    {
        var screening = await counterTicketRepository.GetScreeningByIdAsync(screeningId);
        if (screening == null || screening.Status != "Scheduled" || screening.StartAt <= DateTime.UtcNow || screening.Auditorium.Status != "Active")
        {
            return (null, []);
        }

        var seats = await counterTicketRepository.GetScreeningSeatsAsync(screeningId);
        return (screening, seats);
    }

    public Task<List<ConcessionItem>> GetAvailableConcessionsAsync() =>
        counterTicketRepository.GetAvailableConcessionItemsAsync();

    public Task<CounterReceiptResult?> GetReceiptAsync(int bookingId) =>
        counterTicketRepository.GetReceiptAsync(bookingId);

    public async Task<ServiceResult<CounterSaleResult>> SellTicketsAsync(SellCounterTicketsRequest request)
    {
        // 1. Xác thực thông tin nhân viên thu ngân
        var staff = await counterTicketRepository.GetStaffByUserIdAsync(request.StaffUserId);
        if (staff == null)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty,
                "Tài khoản hiện tại không có thông tin nhân viên để bán vé tại quầy.");
        }

        // 2. Kiểm tra phương thức thanh toán hợp lệ (chỉ chấp nhận tiền mặt hoặc chuyển khoản tại quầy)
        var validMethods = new[] { "Cash", "VNPay_Simulated" };
        if (string.IsNullOrWhiteSpace(request.PaymentMethod) ||
            !validMethods.Contains(request.PaymentMethod.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Phương thức thanh toán không hợp lệ.");
        }

        // 3. Kiểm tra danh sách ghế
        if (request.SeatIds == null || request.SeatIds.Count == 0)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Vui lòng chọn ít nhất một ghế để bán vé.");
        }

        // Chống gửi trùng lặp ID ghế
        if (request.SeatIds.Distinct().Count() != request.SeatIds.Count)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Dữ liệu không hợp lệ: phát hiện ghế bị trùng lặp.");
        }

        // 4. Kiểm tra suất chiếu còn hợp lệ và chưa bắt đầu
        var now = DateTime.UtcNow;
        var screening = await counterTicketRepository.GetScreeningByIdAsync(request.ScreeningId);
        if (screening == null || screening.Status != "Scheduled" || screening.StartAt <= now)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Suất chiếu không hợp lệ hoặc đã qua thời gian bắt đầu.");
        }

        if (screening.Auditorium.Status != "Active")
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Phòng chiếu hiện đang ngừng hoạt động.");
        }

        // 5. Kiểm tra trạng thái thực tế của các ghế được chọn
        var dbSeats = await counterTicketRepository.GetSeatsForSaleAsync(request.ScreeningId, request.SeatIds);
        if (dbSeats.Count != request.SeatIds.Count)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Một số ghế được chọn không tồn tại hoặc không thuộc suất chiếu này.");
        }

        var unavailableSeat = dbSeats.FirstOrDefault(s => s.Status != "Available" || s.Seat.Status != "Active");
        if (unavailableSeat != null)
        {
            string seatName = $"{unavailableSeat.Seat.RowLabel}{unavailableSeat.Seat.SeatNumber}";
            string reason = unavailableSeat.Status switch
            {
                "Held" => "đang được giữ chỗ online",
                "Sold" => "đã được bán",
                _ => "đang bị khóa"
            };
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, $"Ghế {seatName} hiện không ở trạng thái sẵn sàng để bán ({reason}).");
        }

        // 6. Kiểm tra bắp nước (nếu có chọn mua kèm)
        if (request.ConcessionOrders != null && request.ConcessionOrders.Count > 0)
        {
            if (request.ConcessionOrders.GroupBy(c => c.ItemId).Any(g => g.Count() > 1))
            {
                return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Dữ liệu không hợp lệ: phát hiện món bắp nước bị trùng lặp.");
            }

            if (request.ConcessionOrders.Any(c => c.Quantity <= 0))
            {
                return ServiceResult<CounterSaleResult>.Fail(string.Empty, "Số lượng từng món bắp nước phải lớn hơn 0.");
            }
        }

        // 7. Tính tổng tiền vé từ giá vé gốc trong database (không tin giá từ client)
        decimal ticketTotal = dbSeats.Sum(s => s.Price);

        // 8. Thực thi giao dịch bán vé và bắp nước nguyên tử trong 1 Transaction
        try
        {
            var result = await counterTicketRepository.ExecuteCounterSaleTransactionAsync(
                request.ScreeningId,
                request.SeatIds,
                request.ConcessionOrders,
                staff.StaffId,
                ticketTotal,
                request.PaymentMethod.Trim());

            string message = result.ConcessionSale != null
                ? $"Thanh toán thành công! Mã vé: {result.Booking.BookingCode}, Mã bắp nước: {result.ConcessionSale.SaleCode}."
                : $"Bán vé thành công! Mã vé: {result.Booking.BookingCode}, Tổng tiền: {ticketTotal:N0} đ.";

            return ServiceResult<CounterSaleResult>.Success(result, message);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty, ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty,
                "Dữ liệu tồn kho hoặc trạng thái ghế vừa thay đổi do có giao dịch đồng thời. Vui lòng thử lại.");
        }
        catch (Exception)
        {
            return ServiceResult<CounterSaleResult>.Fail(string.Empty,
                "Có lỗi xảy ra trong quá trình lưu dữ liệu bán vé. Vui lòng thử lại sau.");
        }
    }
}
