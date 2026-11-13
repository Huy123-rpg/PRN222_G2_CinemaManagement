using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public sealed record CounterConcessionOrder(int ItemId, int Quantity);
public sealed record CounterSaleResult(Booking Booking, ConcessionSale? ConcessionSale);
public sealed record CounterReceiptResult(Booking Booking, ConcessionSale? ConcessionSale);

/// <summary>
/// Data access contract for Counter Ticket Sales (UC-13).
/// Chuẩn Lab 2 PRN222: Interface ngắn gọn, không phát sinh hàm thừa.
/// </summary>
public interface ICounterTicketRepository
{
    Task<List<Movie>> GetShowingMoviesAsync(string? statusFilter = null);
    Task<List<Screening>> GetAvailableScreeningsAsync(int? movieId, string? statusFilter = null);
    Task<(List<Screening> Items, int TotalCount)> GetAvailableScreeningsPagedAsync(int? movieId, int page, int pageSize, string? statusFilter = null);
    Task<Screening?> GetScreeningByIdAsync(int screeningId);
    Task<List<ScreeningSeat>> GetScreeningSeatsAsync(int screeningId);
    Task<List<ScreeningSeat>> GetSeatsForSaleAsync(int screeningId, IEnumerable<int> seatIds);
    Task<List<ConcessionItem>> GetAvailableConcessionItemsAsync();
    Task<Staff?> GetStaffByUserIdAsync(int userId);
    Task<Booking?> GetActivePendingCounterBookingAsync(int bookingId);
    Task<CounterReceiptResult?> GetReceiptAsync(int bookingId);
    Task<HoldResultViewModel> HoldCounterSeatsAsync(
        int staffId,
        int screeningId,
        int? bookingId,
        int[] seatIds,
        CancellationToken ct = default);
    Task ReleaseCounterSeatsAsync(int staffId, int bookingId, CancellationToken ct = default);
    Task<CounterSaleResult> ExecuteCounterSaleTransactionAsync(
        int screeningId,
        List<int> seatIds,
        List<CounterConcessionOrder>? concessionOrders,
        int staffId,
        decimal ticketTotal,
        string paymentMethod,
        int? holdingBookingId = null);
}
