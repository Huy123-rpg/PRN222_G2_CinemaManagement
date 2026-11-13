using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.ViewModels;

/// <summary>
/// Dữ liệu hiển thị danh mục phim dạng thẻ (Card) tại quầy vé (UC-13).
/// </summary>
public class CounterMovieCatalogViewModel
{
    public List<CounterMovieCardViewModel> Movies { get; set; } = new();
    public string? Search { get; set; }
    public string StatusFilter { get; set; } = "All"; // "All", "Showing", "Upcoming" (Presale)
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public int TotalItems { get; set; }
    public int TotalPages => TotalItems <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    public int TotalShowingCount { get; set; }
    public int TotalUpcomingCount { get; set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All");
}

/// <summary>
/// Thẻ thông tin một bộ phim có suất chiếu khả dụng tại quầy.
/// </summary>
public class CounterMovieCardViewModel
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string AgeRating { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
    public string Status { get; set; } = string.Empty; // "Showing" or "Upcoming"
    public int AvailableScreeningsCount { get; set; }
    public List<string> Genres { get; set; } = new();
}

/// <summary>
/// Dữ liệu màn hình xem danh sách các suất chiếu của một phim tại quầy.
/// </summary>
public class CounterMovieScreeningsViewModel
{
    public Movie Movie { get; set; } = null!;
    public string? SelectedDate { get; set; } // "yyyy-MM-dd"
    public List<DateOnly> AvailableDates { get; set; } = new();
    public List<Screening> Screenings { get; set; } = new();
}

/// <summary>
/// Dữ liệu màn hình lịch sử bán vé tại quầy của nhân viên.
/// </summary>
public class CounterSalesHistoryViewModel
{
    public List<CounterSaleHistoryItem> Items { get; set; } = new();
    public string? SearchCode { get; set; }
    public string? FromDate { get; set; } // "yyyy-MM-dd"
    public string? ToDate { get; set; }   // "yyyy-MM-dd"
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => TotalItems <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    public decimal TotalRevenue { get; set; }
    public int StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
}

/// <summary>
/// Một dòng giao dịch bán vé tại quầy trong lịch sử.
/// </summary>
public class CounterSaleHistoryItem
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public DateTime BookingDate { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
    public DateTime ScreeningStartAt { get; set; }
    public string AuditoriumName { get; set; } = string.Empty;
    public List<string> Seats { get; set; } = new();
    public int TicketCount { get; set; }
    public decimal TicketAmount { get; set; }
    public decimal ConcessionAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string Status { get; set; } = "Confirmed";
    public int? ConcessionSaleId { get; set; }
    public string? ConcessionSaleCode { get; set; }
}
