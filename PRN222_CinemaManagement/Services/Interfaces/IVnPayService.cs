using PRN222_CinemaManagement.Models.DTOs;

namespace PRN222_CinemaManagement.Services.Interfaces;

public interface IVnPayService
{
    string CreatePaymentUrl(int bookingId, decimal amount, string orderInfo, string ipAddress);
    VnPayCallbackResult VerifyCallback(IReadOnlyDictionary<string, string> queryParams);
    string HmacSha512(string key, string inputData);
}

