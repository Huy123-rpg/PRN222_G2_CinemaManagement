using PRN222_CinemaManagement.Services.IService;
using QRCoder;

namespace PRN222_CinemaManagement.Services.ImpleService;

public class QrCodeService : IQrCodeService
{
    public byte[] GeneratePng(string content, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var image = new PngByteQRCode(data);
        return image.GetGraphic(pixelsPerModule);
    }
}

