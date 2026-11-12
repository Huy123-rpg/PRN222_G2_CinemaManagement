using System.ComponentModel.DataAnnotations;
using PRN222_CinemaManagement.Constants;

namespace PRN222_CinemaManagement.Models.ViewModels;

// Dữ liệu hiển thị một phim trong danh sách phim đang chiếu.
public class MovieCardViewModel
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string AgeRating { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public string? PosterUrl { get; set; }
    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();
    public int UpcomingShowtimeCount { get; set; }
}

public class MovieFilterViewModel
{
    public string? Search { get; set; }
    public int? GenreId { get; set; }
    public string? AgeRating { get; set; }
    public DateOnly? ShowDate { get; set; }
    public string SortBy { get; set; } = BookingConstants.MovieSortOptions.TitleAsc;
    public bool OnlyWithShowtimes { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = BookingConstants.DefaultMoviePageSize;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || GenreId.HasValue
        || !string.IsNullOrWhiteSpace(AgeRating)
        || ShowDate.HasValue
        || OnlyWithShowtimes
        || (!string.IsNullOrWhiteSpace(SortBy) && SortBy != BookingConstants.MovieSortOptions.TitleAsc);
}

public class PaginationViewModel
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = BookingConstants.DefaultMoviePageSize;
    public int TotalItems { get; set; }

    public int TotalPages => TotalItems <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public int StartItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int EndItem => Math.Min(Page * PageSize, TotalItems);

    public IReadOnlyList<int> GetPageNumbers(int radius = 2)
    {
        var total = TotalPages;
        var start = Math.Max(1, Page - radius);
        var end = Math.Min(total, Page + radius);
        return Enumerable.Range(start, end - start + 1).ToList();
    }
}

public class GenreOptionViewModel
{
    public int GenreId { get; set; }
    public string GenreName { get; set; } = string.Empty;
    public int MovieCount { get; set; }
}

public class ShowDateOptionViewModel
{
    public DateOnly Date { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class MovieCatalogViewModel
{
    public MovieFilterViewModel Filter { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
    public IReadOnlyList<MovieCardViewModel> Movies { get; set; } = Array.Empty<MovieCardViewModel>();
    public IReadOnlyList<GenreOptionViewModel> Genres { get; set; } = Array.Empty<GenreOptionViewModel>();
    public IReadOnlyList<string> AgeRatings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ShowDateOptionViewModel> ShowDates { get; set; } = Array.Empty<ShowDateOptionViewModel>();
    public int TotalShowingCount { get; set; }
    // filter theo thể loại mặc định của account 
    public string? SelectedGenreName => Filter.GenreId.HasValue
        ? Genres.FirstOrDefault(g => g.GenreId == Filter.GenreId.Value)?.GenreName
        : null;
}

public class MyBookingsViewModel
{
    public IReadOnlyList<BookingDetailsViewModel> Bookings { get; set; } = Array.Empty<BookingDetailsViewModel>();
    public PaginationViewModel Pagination { get; set; } = new();
}

// Thông tin một suất chiếu để khách hàng chọn giờ xem.
public class ShowtimeOptionViewModel
{
    public int ShowtimeId { get; set; }
    public string Auditorium { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public decimal BasePrice { get; set; }
}

public class MovieDetailsViewModel
{
    public MovieCardViewModel Movie { get; set; } = new MovieCardViewModel();
    public List<ShowtimeOptionViewModel> Showtimes { get; set; } = new List<ShowtimeOptionViewModel>();
}

// IsMine cho biết ghế có thuộc đơn đang giữ của khách hiện tại hay không.
public class SeatViewModel
{
    public int ShowtimeSeatId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string SeatType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsMine { get; set; }
}

public class SeatMapViewModel
{
    public int ShowtimeId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Auditorium { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public List<SeatViewModel> Seats { get; set; } = new List<SeatViewModel>();
    public int? BookingId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
}

public class BookingSeatViewModel
{
    public int ShowtimeSeatId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class BookingConcessionViewModel
{
    public int ConcessionItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal => UnitPrice * Quantity;
}

public class TicketViewModel
{
    public int TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
}

// Dùng chung cho trang xác nhận, thanh toán và xem vé của chính khách hàng.
public class BookingDetailsViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int ShowtimeId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Auditorium { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
    public decimal TotalAmount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<BookingSeatViewModel> Seats { get; set; } = new List<BookingSeatViewModel>();
    public List<BookingConcessionViewModel> Concessions { get; set; } = new List<BookingConcessionViewModel>();
    public List<TicketViewModel> Tickets { get; set; } = new List<TicketViewModel>();
    public string? ConcessionTicketCode { get; set; }

    public decimal SeatSubtotal => Seats.Sum(s => s.Price);
    public decimal ConcessionSubtotal => Concessions.Sum(c => c.Subtotal);
}

public class ConcessionOptionViewModel
{
    public int ConcessionItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int MaxSelectableQuantity { get; set; }
    public int SelectedQuantity { get; set; }
}


public class ConcessionSelectionViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int ShowtimeId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Auditorium { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
    public List<BookingSeatViewModel> Seats { get; set; } = new List<BookingSeatViewModel>();
    public List<ConcessionOptionViewModel> Items { get; set; } = new List<ConcessionOptionViewModel>();
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();

    public decimal SeatSubtotal => Seats.Sum(s => s.Price);
    public decimal ConcessionSubtotal => Items.Sum(i => i.Price * i.SelectedQuantity);
    public decimal TotalAmount => SeatSubtotal + ConcessionSubtotal;
}

public class ConcessionOrderItemInput
{
    [Range(1, int.MaxValue)]
    public int ConcessionItemId { get; set; }

    [Range(0, BookingConstants.MaxQuantityPerConcessionItem)]
    public int Quantity { get; set; }
}

// Chỉ nhận ID sản phẩm và số lượng; đơn giá luôn đọc từ ConcessionItem trong DB.
public class SetConcessionsRequest
{
    [Range(1, int.MaxValue)]
    public int BookingId { get; set; }

    [MaxLength(100)]
    public List<ConcessionOrderItemInput> Items { get; set; } = new List<ConcessionOrderItemInput>();
}

public class ConcessionSelectionResultViewModel
{
    public int BookingId { get; set; }
    public decimal SeatSubtotal { get; set; }
    public decimal ConcessionSubtotal { get; set; }
    public decimal TotalAmount { get; set; }
}

// Kết quả giữ ghế do server tính, gồm hạn giữ cố định và tổng tiền.
public class HoldResultViewModel
{
    public int? BookingId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
    public decimal TotalAmount { get; set; }
}

// Chỉ nhận ID ghế; không nhận giá tiền hoặc người mua từ trình duyệt.
public class SetSeatsRequest
{
    [Range(1, int.MaxValue)]
    public int ShowtimeId { get; set; }

    [Range(1, int.MaxValue)]
    public int? BookingId { get; set; }

    [Required, MaxLength(20)]
    public int[] ShowtimeSeatIds { get; set; } = Array.Empty<int>(); 
}

public class SimulatedPaymentRequest
{
    [Range(1, int.MaxValue)]
    public int BookingId { get; set; }

    [Required, RegularExpression("^(Success|Failed|Cancelled)$")]
    public string Outcome { get; set; } = string.Empty;

    [Required, RegularExpression("^(VNPay_Simulated|MoMo_Simulated)$")]
    public string Method { get; set; } = "VNPay_Simulated";
}

public class PaymentFailedViewModel
{
    public BookingDetailsViewModel? Booking { get; set; }
    public string FailureMessage { get; set; } = string.Empty;
    public string? ResponseCode { get; set; }
    public string? TransactionNo { get; set; }
    public bool CanRetry => Booking != null
        && Booking.Status == "Pending"
        && Booking.ExpiresAt > Booking.ServerNow
        && Booking.Seats.Count > 0;
}

public static class BookingDisplay
{
    // Dữ liệu lưu theo UTC, giao diện hiển thị giờ Việt Nam (UTC+7).
    public static DateTime VietnamTime(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(BookingConstants.VietnamUtcOffsetHours);
    }

    public static string StatusText(string status)
    {
        switch (status)
        {
            case "Pending": return "Chờ thanh toán";
            case "Confirmed": return "Đã thanh toán";
            case "Cancelled": return "Đã hủy";
            case "Expired": return "Hết hạn";
            case "Refunded": return "Đã hoàn tiền";
            default: return status;
        }
    }
}
