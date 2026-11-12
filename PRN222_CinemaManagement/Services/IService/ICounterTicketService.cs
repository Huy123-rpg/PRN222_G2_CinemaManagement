using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Services.IService;

public sealed record SellCounterTicketsRequest(
    int StaffUserId,
    int ScreeningId,
    List<int> SeatIds,
    List<CounterConcessionOrder>? ConcessionOrders,
    string PaymentMethod);

/// <summary>
/// Business logic contract for Counter Ticket Sales (UC-13).
/// Chuẩn Lab 2 PRN222: Interface tinh gọn, chỉ chứa các phương thức nghiệp vụ cần thiết.
/// </summary>
public interface ICounterTicketService
{
    Task<List<Movie>> GetShowingMoviesAsync();
    Task<List<Screening>> GetAvailableScreeningsAsync(int? movieId);
    Task<(Screening? Screening, List<ScreeningSeat> Seats)> GetScreeningSeatsMapAsync(int screeningId);
    Task<List<ConcessionItem>> GetAvailableConcessionsAsync();
    Task<CounterReceiptResult?> GetReceiptAsync(int bookingId);
    Task<ServiceResult<CounterSaleResult>> SellTicketsAsync(SellCounterTicketsRequest request);
}
