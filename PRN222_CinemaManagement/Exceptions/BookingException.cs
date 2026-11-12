namespace PRN222_CinemaManagement.Exceptions;

/// <summary>Lỗi nghiệp vụ có thể hiển thị trực tiếp bằng tiếng Việt; không lộ lỗi SQL ra ngoài.</summary>
public class BookingException(string message, int statusCode = 409) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

