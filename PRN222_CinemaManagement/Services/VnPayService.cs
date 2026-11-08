using System.Net; // Nhớ using thư viện này
using System.Security.Cryptography;
using System.Text;

namespace PRN222_CinemaManagement.Services
{
    public class VnPayService
    {
        private readonly IConfiguration _config;

        public VnPayService(IConfiguration config)
        {
            _config = config;
        }

        public string CreatePaymentUrl(int bookingId, decimal amount, string orderInfo, string ipAddress)
        {
            var vnp_TmnCode = _config["VnPay:TmnCode"];
            var vnp_HashSecret = _config["VnPay:HashSecret"];
            var vnp_BaseUrl = _config["VnPay:BaseUrl"];
            var vnp_ReturnUrl = _config["VnPay:ReturnUrl"];

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
                { "vnp_TxnRef", bookingId.ToString() }
            };

            // 1. Tạo chuỗi ĐÃ MÃ HÓA URL (Dùng WebUtility.UrlEncode)
            var queryString = string.Join("&", vnp_Params.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));

            // 2. Tính mã băm (Hash) TRÊN CHÍNH CHUỖI ĐÃ MÃ HÓA NÀY
            var secureHash = HmacSha512(vnp_HashSecret!, queryString);

            // 3. Trả về URL hoàn chỉnh
            return $"{vnp_BaseUrl}?{queryString}&vnp_SecureHash={secureHash}";
        }

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
}