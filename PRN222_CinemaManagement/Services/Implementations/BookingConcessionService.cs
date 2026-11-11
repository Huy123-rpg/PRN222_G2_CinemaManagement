using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Exceptions;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Repositories.Interfaces;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Services.Implementations;

/// <summary>Hiển thị menu bắp/nước và lưu lựa chọn ăn uống cho đơn đặt vé đang giữ ghế.</summary>
public class BookingConcessionService(
    IBookingRepository bookingRepository,
    IShowtimeSeatRepository showtimeSeatRepository,
    IConcessionRepository concessionRepository,
    IBookingConcessionRepository bookingConcessionRepository,
    IBookingSupportService support,
    IUnitOfWork unitOfWork) : IBookingConcessionService
{
    private static readonly string[] CategoryOrder = ["Combo", "Bắp rang", "Đồ uống", "Món ăn vặt"];

    public async Task<ConcessionSelectionViewModel?> GetSelectionPageAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        var booking = await bookingRepository.GetOwnedOnlineBookingDetailsAsync(customerId, bookingId, ct);
        if (booking == null) return null;

        var activeItems = await concessionRepository.GetActiveItemsAsync(ct);
        var selectedQuantities = booking.BookingConcessions
            .ToDictionary(bc => bc.ConcessionItemId, bc => bc.Quantity);

        var serverNow = DateTime.UtcNow;
        var status = booking.Status;
        if (status == BookingConstants.BookingStatuses.Pending && booking.ExpiresAt <= serverNow)
        {
            status = BookingConstants.BookingStatuses.Expired;
        }

        var items = activeItems
            .Select(item =>
            {
                var selectedQty = selectedQuantities.TryGetValue(item.ConcessionItemId, out var qty) ? qty : 0;
                return new ConcessionOptionViewModel
                {
                    ConcessionItemId = item.ConcessionItemId,
                    Name = item.Name,
                    Description = item.Description,
                    Category = ResolveCategory(item.Name, item.Description),
                    Price = item.Price,
                    StockQuantity = item.StockQuantity,
                    MaxSelectableQuantity = Math.Min(item.StockQuantity, BookingConstants.MaxQuantityPerConcessionItem),
                    SelectedQuantity = Math.Min(selectedQty, Math.Min(item.StockQuantity, BookingConstants.MaxQuantityPerConcessionItem))
                };
            })
            .OrderBy(i => CategoryRank(i.Category))
            .ThenBy(i => i.Price)
            .ThenBy(i => i.ConcessionItemId)
            .ToList();

        var categories = items
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(CategoryRank)
            .ToList();

        return new ConcessionSelectionViewModel
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            ShowtimeId = booking.ShowtimeId,
            MovieTitle = booking.Showtime.Movie.Title,
            Auditorium = booking.Showtime.Auditorium.Name,
            StartAt = support.AsUtc(booking.Showtime.StartAt),
            Status = status,
            ExpiresAt = support.AsUtc(booking.ExpiresAt),
            ServerNow = serverNow,
            Seats = booking.HeldShowtimeSeats
                .OrderBy(s => s.Seat.RowLabel)
                .ThenBy(s => s.Seat.SeatNumber)
                .Select(s => new BookingSeatViewModel
                {
                    ShowtimeSeatId = s.ShowtimeSeatId,
                    Label = s.Seat.RowLabel + s.Seat.SeatNumber,
                    SeatType = s.Seat.SeatType.TypeName,
                    Price = s.Price
                })
                .ToList(),
            Items = items,
            Categories = categories
        };
    }

    public async Task<ConcessionSelectionResultViewModel> SaveSelectionAsync(
        int userId,
        SetConcessionsRequest request,
        CancellationToken ct = default)
    {
        var customerId = await support.GetActiveCustomerIdAsync(userId, ct);
        if (request == null || request.BookingId <= 0 || request.Items == null)
        {
            throw new BookingException("Dữ liệu bắp nước không hợp lệ.", 400);
        }

        if (request.Items.GroupBy(i => i.ConcessionItemId).Any(g => g.Count() > 1))
        {
            throw new BookingException("Danh sách sản phẩm bắp nước bị trùng lặp.", 400);
        }

        var requestedItems = request.Items
            .Where(i => i.Quantity > 0)
            .ToList();

        if (requestedItems.Any(i => i.ConcessionItemId <= 0
                || i.Quantity > BookingConstants.MaxQuantityPerConcessionItem))
        {
            throw new BookingException($"Số lượng mỗi món phải từ 1 đến {BookingConstants.MaxQuantityPerConcessionItem}.", 400);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var booking = await support.LockOwnedBookingAsync(customerId, request.BookingId, ct);
            support.EnsurePendingBooking(booking);

            var heldSeats = await showtimeSeatRepository.GetHeldSeatsByBookingIdAsync(booking.BookingId, ct);
            if (heldSeats.Count == 0 || heldSeats.Any(s => s.HoldExpiresAt <= DateTime.UtcNow))
            {
                throw new BookingException("Bạn chưa giữ ghế hợp lệ hoặc đã hết thời gian giữ ghế. Vui lòng chọn ghế lại.");
            }

            await bookingConcessionRepository.DeleteByBookingIdAsync(booking.BookingId, ct);

            decimal concessionTotal = 0m;
            if (requestedItems.Count > 0)
            {
                var dbItems = await concessionRepository.GetActiveItemsByIdsAsync(
                    requestedItems.Select(i => i.ConcessionItemId),
                    ct);
                var itemById = dbItems.ToDictionary(i => i.ConcessionItemId);

                var bookingConcessions = new List<BookingConcession>(requestedItems.Count);
                foreach (var requested in requestedItems)
                {
                    if (!itemById.TryGetValue(requested.ConcessionItemId, out var dbItem))
                    {
                        throw new BookingException("Sản phẩm bắp/nước không tồn tại hoặc đã ngừng bán.", 400);
                    }

                    if (requested.Quantity > dbItem.StockQuantity)
                    {
                        throw new BookingException($"'{dbItem.Name}' hiện chỉ còn {dbItem.StockQuantity} phần.", 400);
                    }

                    bookingConcessions.Add(new BookingConcession
                    {
                        BookingId = booking.BookingId,
                        ConcessionItemId = dbItem.ConcessionItemId,
                        Quantity = requested.Quantity,
                        UnitPrice = dbItem.Price
                    });
                    concessionTotal += dbItem.Price * requested.Quantity;
                }

                await bookingConcessionRepository.AddRangeAsync(bookingConcessions, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }

            var seatTotal = heldSeats.Sum(s => s.Price);
            var grandTotal = seatTotal + concessionTotal;
            var updated = await bookingRepository.UpdatePendingTotalAmountIfValidAsync(
                booking.BookingId,
                grandTotal,
                DateTime.UtcNow,
                ct);

            if (updated != 1)
            {
                throw new BookingException("Đã hết thời gian giữ ghế hoặc suất chiếu đã đóng.");
            }

            await transaction.CommitAsync(ct);

            return new ConcessionSelectionResultViewModel
            {
                BookingId = booking.BookingId,
                SeatSubtotal = seatTotal,
                ConcessionSubtotal = concessionTotal,
                TotalAmount = grandTotal
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            unitOfWork.ClearChangeTracker();
            throw;
        }
    }

    private static string ResolveCategory(string name, string? description)
    {
        var text = $"{name} {description}".ToLowerInvariant();
        if (text.Contains("combo")) return "Combo";
        if (text.Contains("bắp") || text.Contains("popcorn")) return "Bắp rang";
        if (text.Contains("coca") || text.Contains("sprite") || text.Contains("7up")
            || text.Contains("pepsi") || text.Contains("trà") || text.Contains("nước")
            || text.Contains("dasani"))
        {
            return "Đồ uống";
        }
        return "Món ăn vặt";
    }

    private static int CategoryRank(string category)
    {
        var index = Array.IndexOf(CategoryOrder, category);
        return index >= 0 ? index : CategoryOrder.Length;
    }
}

