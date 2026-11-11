namespace PRN222_CinemaManagement.Services.Interfaces;

public interface IQrCodeService
{
    byte[] GeneratePng(string content, int pixelsPerModule = 8);
}

