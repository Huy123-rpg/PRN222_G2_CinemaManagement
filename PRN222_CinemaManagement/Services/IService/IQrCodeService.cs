namespace PRN222_CinemaManagement.Services.IService;

public interface IQrCodeService
{
    byte[] GeneratePng(string content, int pixelsPerModule = 8);
}

