using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public sealed record SellCounterTicketsRequest(
    int StaffUserId,
    int ScreeningId,
    List<int> SeatIds,
    List<CounterConcessionOrder>? ConcessionOrders,
    string PaymentMethod,
    int? HoldingBookingId = null);

/// <summary>
/// Business logic contract for Counter Ticket Sales (UC-13).
/// Chuẩn Lab 2 PRN222: Interface tinh gọn, chỉ chứa các phương thức nghiệp vụ cần thiết.
/// </summary>
public interface ICounterTicketService
{
    Task<List<Movie>> GetShowingMoviesAsync(string? statusFilter = null);
    Task<List<Screening>> GetAvailableScreeningsAsync(int? movieId, string? statusFilter = null);
    Task<(List<Screening> Items, int TotalCount)> GetAvailableScreeningsPagedAsync(int? movieId, int page, int pageSize, string? statusFilter = null);
    Task<(Screening? Screening, List<ScreeningSeat> Seats)> GetScreeningSeatsMapAsync(int screeningId);
    Task<List<ConcessionItem>> GetAvailableConcessionsAsync();
    Task<Booking?> GetActivePendingCounterBookingAsync(int bookingId);
    Task<CounterReceiptResult?> GetReceiptAsync(int bookingId);
    Task<ServiceResult<HoldResultViewModel>> HoldCounterSeatsAsync(
        int staffUserId,
        int screeningId,
        int? bookingId,
        int[] seatIds,
        CancellationToken ct = default);
    Task<ServiceResult> ReleaseCounterSeatsAsync(int staffUserId, int bookingId, CancellationToken ct = default);
    Task<CounterMovieCatalogViewModel> GetCounterMovieCatalogAsync(string? search, string? statusFilter, int page, int pageSize);
    Task<CounterMovieScreeningsViewModel?> GetMovieScreeningsForCounterAsync(int movieId, string? showDateStr);
    Task<CounterSalesHistoryViewModel> GetCounterSalesHistoryAsync(int staffUserId, string? searchCode, string? fromDateStr, string? toDateStr, int page, int pageSize);
    Task<ServiceResult<CounterSaleResult>> SellTicketsAsync(SellCounterTicketsRequest request);
}
