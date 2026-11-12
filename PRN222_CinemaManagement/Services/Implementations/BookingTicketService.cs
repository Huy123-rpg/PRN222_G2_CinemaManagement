using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Repositories.Interfaces;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Services.Implementations;

/// <summary>Đọc đơn của khách hàng, hiển thị vé và sinh ảnh QR. Không xử lý thanh toán.</summary>
public class BookingTicketService(
    IBookingRepository bookingRepository,
    ITicketRepository ticketRepository,
    IBookingSupportService support,
    IQrCodeService qrCodeService) : IBookingTicketService
{
    public async Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var booking = await bookingRepository.GetOwnedOnlineBookingDetailsAsync(customerId, bookingId, ct);
        return booking == null ? null : CreateBookingViewModel(booking);
    }

    public async Task<BookingDetailsViewModel?> GetBookingDetailsByIdAsync(int bookingId, CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetOnlineBookingDetailsByIdAsync(bookingId, ct);
        return booking == null ? null : CreateBookingViewModel(booking);
    }

    public async Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var bookings = await bookingRepository.GetOwnedOnlineBookingsDetailsAsync(customerId, ct);
        return bookings.Select(CreateBookingViewModel).ToList();
    }

    public async Task<MyBookingsViewModel> GetMyBookingsPagedAsync(
        int userId,
        int page = 1,
        int pageSize = BookingConstants.DefaultBookingPageSize,
        CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var normalizedSize = Math.Clamp(pageSize, 1, BookingConstants.MaxPageSize);
        var (bookings, totalCount) = await bookingRepository.GetPagedOwnedOnlineBookingsDetailsAsync(
            customerId,
            page,
            normalizedSize,
            ct);

        var totalPages = totalCount <= 0 ? 1 : (int)Math.Ceiling((double)totalCount / normalizedSize);
        var currentPage = Math.Clamp(page, 1, totalPages);

        return new MyBookingsViewModel
        {
            Bookings = bookings.Select(CreateBookingViewModel).ToList(),
            Pagination = new PaginationViewModel
            {
                Page = currentPage,
                PageSize = normalizedSize,
                TotalItems = totalCount
            }
        };
    }

    public async Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var ticket = await ticketRepository.GetConfirmedOnlineTicketForQrAsync(customerId, ticketId, ct);
        return ticket == null ? null : qrCodeService.GeneratePng(ticket.TicketCode);
    }

    public async Task<byte[]?> GetConcessionQrAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var booking = await bookingRepository.GetConfirmedOnlineBookingForConcessionQrAsync(customerId, bookingId, ct);
        return booking == null ? null : qrCodeService.GeneratePng(BuildConcessionTicketCode(booking.BookingCode));
    }

    private BookingDetailsViewModel CreateBookingViewModel(Booking booking)
    {
        var model = new BookingDetailsViewModel
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            ShowtimeId = booking.ShowtimeId,
            MovieTitle = booking.Showtime.Movie.Title,
            Auditorium = booking.Showtime.Auditorium.Name,
            StartAt = support.AsUtc(booking.Showtime.StartAt),
            Status = booking.Status,
            ExpiresAt = support.AsUtc(booking.ExpiresAt),
            ServerNow = DateTime.UtcNow,
            TotalAmount = booking.TotalAmount,
            CustomerName = booking.Customer!.User.FullName,
            Email = booking.Customer.User.Email
        };

        if (booking.Status == BookingConstants.BookingStatuses.Pending && booking.ExpiresAt <= model.ServerNow)
        {
            model.Status = BookingConstants.BookingStatuses.Expired;
        }

        if (booking.Tickets.Count > 0)
        {
            // Sau thanh toán, liên kết giữ ghế đã xóa; lấy ghế từ vé đã phát.
            foreach (var ticket in booking.Tickets)
            {
                var seat = ticket.ShowtimeSeat.Seat;
                model.Seats.Add(new BookingSeatViewModel
                {
                    ShowtimeSeatId = ticket.ShowtimeSeatId,
                    Label = seat.RowLabel + seat.SeatNumber,
                    SeatType = seat.SeatType.TypeName,
                    Price = ticket.Price
                });
                model.Tickets.Add(new TicketViewModel
                {
                    TicketId = ticket.TicketId,
                    TicketCode = ticket.TicketCode,
                    SeatLabel = seat.RowLabel + seat.SeatNumber,
                    Price = ticket.Price,
                    Status = ticket.Status
                });
            }
        }
        else
        {
            foreach (var seat in booking.HeldShowtimeSeats)
            {
                model.Seats.Add(new BookingSeatViewModel
                {
                    ShowtimeSeatId = seat.ShowtimeSeatId,
                    Label = seat.Seat.RowLabel + seat.Seat.SeatNumber,
                    SeatType = seat.Seat.SeatType.TypeName,
                    Price = seat.Price
                });
            }
        }

        foreach (var item in booking.BookingConcessions.OrderBy(bc => bc.BookingConcessionId))
        {
            model.Concessions.Add(new BookingConcessionViewModel
            {
                ConcessionItemId = item.ConcessionItemId,
                Name = item.ConcessionItem?.Name ?? $"Món #{item.ConcessionItemId}",
                Description = item.ConcessionItem?.Description,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity
            });
        }

        if (model.Status == BookingConstants.BookingStatuses.Confirmed && model.Concessions.Count > 0)
        {
            model.ConcessionTicketCode = BuildConcessionTicketCode(booking.BookingCode);
        }

        return model;
    }

    private static string BuildConcessionTicketCode(string bookingCode)
    {
        const string bookingPrefix = "BK-";
        var suffix = bookingCode.StartsWith(bookingPrefix, StringComparison.OrdinalIgnoreCase)
            ? bookingCode[bookingPrefix.Length..]
            : bookingCode;
        return BookingConstants.ConcessionTicketPrefix + suffix;
    }
}

