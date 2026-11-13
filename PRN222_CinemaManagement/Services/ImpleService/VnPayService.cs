using System.Net;
using System.Security.Cryptography;
using System.Text;
using PRN222_CinemaManagement.Models.DTOs;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

public class VnPayService(IConfiguration config) : IVnPayService
{
    public string CreatePaymentUrl(int bookingId, decimal amount, string orderInfo, string ipAddress)
    {
        var vnp_TmnCode = config["VnPay:TmnCode"];
        var vnp_HashSecret = config["VnPay:HashSecret"];
        var vnp_BaseUrl = config["VnPay:BaseUrl"];
        var vnp_ReturnUrl = config["VnPay:ReturnUrl"];

        var vnp_Params = new SortedDictionary<string, string>
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", vnp_TmnCode! },
            { "vnp_Amount", ((long)(amount * 100)).ToString() },
            { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
            { "vnp_CurrCode", "VND" },
            { "vnp_IpAddr", ipAddress },
            { "vnp_Locale", "vn" },
            { "vnp_OrderInfo", orderInfo },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", vnp_ReturnUrl! },
            { "vnp_TxnRef", bookingId.ToString() }, 
            { "vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss") }
        };

        // 1. Tạo chuỗi ĐÃ MÃ HÓA URL (Dùng WebUtility.UrlEncode)
        var queryString = string.Join("&", vnp_Params.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));

        // 2. Tính mã băm (Hash) TRÊN CHÍNH CHUỖI ĐÃ MÃ HÓA NÀY
        var secureHash = HmacSha512(vnp_HashSecret!, queryString);

        // 3. Trả về URL hoàn chỉnh
        return $"{vnp_BaseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    public VnPayCallbackResult VerifyCallback(IReadOnlyDictionary<string, string> queryParams)
    {
        var hashSecret = config["VnPay:HashSecret"];
        if (string.IsNullOrEmpty(hashSecret))
        {
            return new VnPayCallbackResult
            {
                IsValidSignature = false,
                ErrorMessage = "Thiếu cấu hình bảo mật VNPay."
            };
        }

        if (!queryParams.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrEmpty(secureHash))
        {
            return new VnPayCallbackResult
            {
                IsValidSignature = false,
                ErrorMessage = "Chữ ký không hợp lệ (Dữ liệu có thể đã bị thay đổi)."
            };
        }

        var vnpParams = new SortedDictionary<string, string>();
        foreach (var kvp in queryParams)
        {
            if (kvp.Key.StartsWith("vnp_", StringComparison.Ordinal) && kvp.Key != "vnp_SecureHash")
            {
                vnpParams.Add(kvp.Key, kvp.Value);
            }
        }

        // BẮT BUỘC: Phải dùng WebUtility.UrlEncode để mã hóa lại chuỗi trước khi băm
        var queryString = string.Join("&", vnpParams.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));
        var checkSum = HmacSha512(hashSecret, queryString);

        if (!checkSum.Equals(secureHash, StringComparison.InvariantCultureIgnoreCase))
        {
            return new VnPayCallbackResult
            {
                IsValidSignature = false,
                ErrorMessage = "Chữ ký không hợp lệ (Dữ liệu có thể đã bị thay đổi)."
            };
        }

        if (!queryParams.TryGetValue("vnp_TxnRef", out var txnRef) || !int.TryParse(txnRef, out int bookingId))
        {
            return new VnPayCallbackResult
            {
                IsValidSignature = false,
                ErrorMessage = "Mã đơn hàng VNPay không hợp lệ."
            };
        }

        queryParams.TryGetValue("vnp_ResponseCode", out var responseCode);
        queryParams.TryGetValue("vnp_TransactionNo", out var transactionNo);
        var isSuccess = responseCode == "00";

        return new VnPayCallbackResult
        {
            IsValidSignature = true,
            BookingId = bookingId,
            IsSuccess = isSuccess,
            ResponseCode = responseCode,
            TransactionNo = transactionNo,
            ErrorMessage = isSuccess ? null : MapResponseMessage(responseCode)
        };
    }

    private static string MapResponseMessage(string? responseCode) => responseCode switch
    {
        "07" => "Giao dịch bị nghi ngờ gian lận hoặc bất thường theo quy định của VNPay.",
        "09" => "Thẻ hoặc tài khoản ngân hàng chưa đăng ký dịch vụ Internet Banking.",
        "10" => "Thông tin thẻ/tài khoản xác thực không chính xác quá 3 lần.",
        "11" => "Đã hết thời gian chờ thanh toán tại cổng VNPay.",
        "12" => "Thẻ hoặc tài khoản ngân hàng hiện đang bị khóa.",
        "13" => "Mã xác thực OTP không chính xác.",
        "24" => "Bạn đã hủy giao dịch thanh toán trên cổng VNPay.",
        "51" => "Tài khoản không đủ số dư để thực hiện giao dịch này.",
        "65" => "Tài khoản đã vượt quá hạn mức giao dịch trong ngày.",
        "75" => "Ngân hàng thanh toán đang bảo trì. Vui lòng thử lại hoặc đổi ngân hàng.",
        "79" => "Nhập sai mật khẩu thanh toán quá số lần quy định.",
        _ => string.IsNullOrWhiteSpace(responseCode)
            ? "Giao dịch thanh toán VNPay không thành công."
            : $"Giao dịch thanh toán VNPay không thành công (mã lỗi {responseCode})."
    };

    public string HmacSha512(string key, string inputData)
    {
        var hash = new StringBuilder();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);
        using (var hmac = new HMACSHA512(keyBytes))
        {
            var hashValue = hmac.ComputeHash(inputBytes);
            foreach (var theByte in hashValue)
            {
                hash.Append(theByte.ToString("x2"));
            }
        }
        return hash.ToString();
    }
}

