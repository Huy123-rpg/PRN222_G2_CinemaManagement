using QRCoder;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

/// <summary>
/// Sinh mã QR cho vé điện tử. Tách riêng khỏi nghiệp vụ đặt vé để dễ thay thế
/// bằng thư viện khác mà không phải đụng tới logic thanh toán.
/// </summary>
public interface IQrCodeGenerator
{
    /// <summary>Trả về ảnh PNG của mã QR chứa nội dung <paramref name="content"/>.</summary>
    byte[] GeneratePng(string content, int pixelsPerModule = 8);
}

/// <summary>
/// Hiện thực dùng thư viện QRCoder.
/// Nội dung mã là mã vé đã lưu trong bảng Tickets, nên nhân viên rạp có thể quét
/// và tra cứu đúng vé đó trong hệ thống.
/// </summary>
public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public byte[] GeneratePng(string content, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

        // PngByteQRCode tự sinh ảnh PNG; không cần thư viện xử lý ảnh ngoài.
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
