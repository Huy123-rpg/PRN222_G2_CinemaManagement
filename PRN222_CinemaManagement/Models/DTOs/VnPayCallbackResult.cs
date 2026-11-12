namespace PRN222_CinemaManagement.Models.DTOs;

public sealed class VnPayCallbackResult
{
    public bool IsValidSignature { get; init; }
    public bool IsSuccess { get; init; }
    public int BookingId { get; init; }
    public string? ResponseCode { get; init; }
    public string? TransactionNo { get; init; }
    public string? ErrorMessage { get; init; }
}

