namespace PRN222_CinemaManagement.Services.OnlineBooking;

/// <summary>Kết quả trả về của cổng thanh toán giả lập.</summary>
/// <param name="IsSuccess">True khi cổng báo giao dịch thành công.</param>
/// <param name="TransactionRef">Mã giao dịch đối chiếu, dùng làm khóa chống thanh toán lặp.</param>
/// <param name="Message">Thông báo tiếng Việt để hiển thị cho khách.</param>
public record PaymentResult(bool IsSuccess, string TransactionRef, string Message);

/// <summary>
/// Cổng thanh toán giả lập. Trong phạm vi bài tập, kết quả do chính khách hàng chọn
/// (Thành công / Thất bại / Hủy) để mô phỏng cả ba nhánh xử lý thanh toán.
/// Ngoài phạm vi: tích hợp cổng thanh toán thật (VNPay/MoMo), hoàn tiền, đối soát.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(int bookingId, string bookingCode, decimal amount,
        string method, string outcome, CancellationToken ct = default);
}

/// <summary>
/// Hiện thực giả lập của cổng thanh toán.
/// Mỗi lần thu tiền sinh một mã giao dịch riêng; chống thanh toán trùng được bảo đảm
/// ở tầng nghiệp vụ bằng unique index và cơ chế phiên bản của đơn.
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(int bookingId, string bookingCode, decimal amount,
        string method, string outcome, CancellationToken ct = default)
    {
        // Mỗi lần thu tiền là một lần thử riêng biệt nên mã giao dịch phải khác nhau:
        // khách thử lại sau khi thanh toán thất bại vẫn tạo được bản ghi Payment mới.
        // Việc chống thanh toán trùng được bảo đảm ở tầng nghiệp vụ bằng unique index
        // (UX_Payments_Success: mỗi đơn chỉ có một giao dịch thành công) và RowVersion của đơn.
        var unique = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        var transactionRef = $"{method}-{bookingCode}-{unique}";

        return Task.FromResult(outcome switch
        {
            "Success" => new PaymentResult(true, transactionRef,
                "Thanh toán thành công. Vé điện tử đã được phát."),
            "Failed" => new PaymentResult(false, transactionRef,
                "Thanh toán không thành công. Bạn có thể thử lại trong thời gian giữ ghế còn lại."),
            "Cancelled" => new PaymentResult(false, transactionRef,
                "Đã hủy thanh toán. Ghế đã được giải phóng."),
            _ => new PaymentResult(false, transactionRef, "Kết quả thanh toán không hợp lệ.")
        });
    }
}
