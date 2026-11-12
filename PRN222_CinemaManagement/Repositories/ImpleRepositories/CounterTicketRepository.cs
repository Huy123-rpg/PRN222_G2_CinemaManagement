using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

/// <summary>
/// EF Core implementation of <see cref="ICounterTicketRepository"/>.
/// Chuẩn Lab 2 PRN222: Truy vấn LINQ đơn giản, thực thi Database Transaction nguyên tử.
/// </summary>
public class CounterTicketRepository(CinemaDbContext db) : ICounterTicketRepository
{
    public Task<List<Movie>> GetShowingMoviesAsync() =>
        db.Movies
            .AsNoTracking()
            .Where(m => m.Status == "Showing")
            .OrderBy(m => m.Title)
            .ToListAsync();

    public Task<List<Screening>> GetAvailableScreeningsAsync(int? movieId)
    {
        var now = DateTime.UtcNow;
        var query = db.Screenings
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .Where(s => s.Status == "Scheduled"
                     && s.StartAt > now
                     && s.Movie.Status == "Showing"
                     && s.Auditorium.Status == "Active");

        if (movieId.HasValue && movieId.Value > 0)
        {
            query = query.Where(s => s.MovieId == movieId.Value);
        }

        return query.OrderBy(s => s.StartAt).ToListAsync();
    }

    public Task<Screening?> GetScreeningByIdAsync(int screeningId) =>
        db.Screenings
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .FirstOrDefaultAsync(s => s.ScreeningId == screeningId);

    public Task<List<ScreeningSeat>> GetScreeningSeatsAsync(int screeningId) =>
        db.ScreeningSeats
            .AsNoTracking()
            .Include(ss => ss.Seat).ThenInclude(s => s.SeatType)
            .Where(ss => ss.ScreeningId == screeningId)
            .OrderBy(ss => ss.Seat.RowLabel)
            .ThenBy(ss => ss.Seat.SeatNumber)
            .ToListAsync();

    public Task<List<ScreeningSeat>> GetSeatsForSaleAsync(int screeningId, IEnumerable<int> seatIds) =>
        db.ScreeningSeats
            .Include(ss => ss.Seat).ThenInclude(s => s.SeatType)
            .Where(ss => seatIds.Contains(ss.ScreeningSeatId) && ss.ScreeningId == screeningId)
            .ToListAsync();

    public Task<List<ConcessionItem>> GetAvailableConcessionItemsAsync() =>
        db.ConcessionItems
            .AsNoTracking()
            .Where(ci => ci.Status == "Active" && ci.StockQuantity > 0)
            .OrderBy(ci => ci.Name)
            .ToListAsync();

    public Task<Staff?> GetStaffByUserIdAsync(int userId) =>
        db.Staffs.FirstOrDefaultAsync(s => s.UserId == userId);

    public async Task<CounterReceiptResult?> GetReceiptAsync(int bookingId)
    {
        var booking = await db.Bookings
            .AsNoTracking()
            .Include(b => b.Staff).ThenInclude(s => s!.User)
            .Include(b => b.Screening).ThenInclude(sc => sc.Movie)
            .Include(b => b.Screening).ThenInclude(sc => sc.Auditorium)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.ScreeningSeat)
                .ThenInclude(ss => ss.Seat)
                .ThenInclude(s => s.SeatType)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.BookingChannel == "Counter");

        if (booking == null) return null;

        // Tìm chính xác hóa đơn bắp nước liên kết cùng giao dịch (nếu có) thông qua mã SaleCode = CS-{BookingCode}
        string linkedSaleCode = $"CS-{booking.BookingCode}";
        var concessionSale = await db.ConcessionSales
            .AsNoTracking()
            .Include(cs => cs.Staff).ThenInclude(s => s!.User)
            .Include(cs => cs.ConcessionSaleItems).ThenInclude(i => i.ConcessionItem)
            .FirstOrDefaultAsync(cs => cs.SaleCode == linkedSaleCode);

        return new CounterReceiptResult(booking, concessionSale);
    }

    public async Task<CounterSaleResult> ExecuteCounterSaleTransactionAsync(
        int screeningId,
        List<int> seatIds,
        List<CounterConcessionOrder>? concessionOrders,
        int staffId,
        decimal ticketTotal,
        string paymentMethod)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // 1. Cập nhật nguyên tử trạng thái ghế từ Available -> Sold
            // Ràng buộc CK_ScreeningSeats_HeldState yêu cầu HeldByBookingId và HoldExpiresAt phải là NULL khi Status != 'Held'
            var updatedRows = await db.ScreeningSeats
                .Where(ss => seatIds.Contains(ss.ScreeningSeatId)
                          && ss.ScreeningId == screeningId
                          && ss.Status == "Available")
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(s => s.Status, "Sold")
                    .SetProperty(s => s.HeldByBookingId, (int?)null)
                    .SetProperty(s => s.HoldExpiresAt, (DateTime?)null));

            if (updatedRows != seatIds.Count)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    "Giao dịch không thành công: một trong các ghế bạn chọn vừa được mua hoặc giữ bởi người khác. Vui lòng chọn ghế khác.");
            }

            var now = DateTime.UtcNow;

            // 2. Tạo hóa đơn vé (Booking)
            var booking = new Booking
            {
                BookingCode = "CT-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                CustomerId = null, // Khách mua trực tiếp tại quầy
                StaffId = staffId,
                ScreeningId = screeningId,
                BookingDate = now,
                ExpiresAt = now.AddDays(1),
                TotalAmount = ticketTotal,
                BookingChannel = "Counter",
                Status = "Confirmed"
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync(); // Sinh BookingId

            // 3. Tạo vé cho từng ghế
            var dbSeats = await db.ScreeningSeats
                .Where(ss => seatIds.Contains(ss.ScreeningSeatId))
                .ToListAsync();

            foreach (var seat in dbSeats)
            {
                db.Tickets.Add(new Ticket
                {
                    TicketCode = "TK-" + Guid.NewGuid().ToString("N")[..10].ToUpper(),
                    BookingId = booking.BookingId,
                    ScreeningSeatId = seat.ScreeningSeatId,
                    ScreeningId = screeningId,
                    Price = seat.Price,
                    Status = "Valid",
                    IssuedAt = now,
                    CheckedInAt = null
                });
            }

            // 4. Tạo thanh toán cho hóa đơn vé
            db.Payments.Add(new Payment
            {
                BookingId = booking.BookingId,
                PaymentCode = "PM-" + Guid.NewGuid().ToString("N")[..10].ToUpper(),
                Provider = "Counter",
                TransactionRef = (paymentMethod == "Cash" ? "CASH-" : "VNP-") + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                Amount = ticketTotal,
                PaymentMethod = paymentMethod,
                PaymentStatus = "Success",
                CreatedAt = now,
                PaidAt = now
            });

            // 5. Nếu khách có mua kèm bắp nước: tạo hóa đơn bắp nước (ConcessionSale) và trừ kho
            ConcessionSale? concessionSale = null;
            if (concessionOrders != null && concessionOrders.Count > 0)
            {
                var itemIds = concessionOrders.Select(c => c.ItemId).ToList();
                var dbItems = await db.ConcessionItems
                    .Where(ci => itemIds.Contains(ci.ConcessionItemId) && ci.Status == "Active")
                    .ToListAsync();

                if (dbItems.Count != itemIds.Count)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Có sản phẩm bắp nước không tồn tại hoặc đã ngừng kinh doanh.");
                }

                decimal concessionTotal = 0m;
                var saleItems = new List<ConcessionSaleItem>();

                foreach (var order in concessionOrders)
                {
                    var dbItem = dbItems.First(ci => ci.ConcessionItemId == order.ItemId);
                    if (dbItem.StockQuantity < order.Quantity)
                    {
                        await transaction.RollbackAsync();
                        throw new InvalidOperationException(
                            $"Sản phẩm '{dbItem.Name}' không đủ tồn kho (còn {dbItem.StockQuantity}, yêu cầu {order.Quantity}).");
                    }

                    // Trừ tồn kho từ cơ sở dữ liệu
                    dbItem.StockQuantity -= order.Quantity;
                    decimal subtotal = dbItem.Price * order.Quantity;
                    concessionTotal += subtotal;

                    saleItems.Add(new ConcessionSaleItem
                    {
                        ConcessionItemId = dbItem.ConcessionItemId,
                        Quantity = order.Quantity,
                        UnitPrice = dbItem.Price,
                        Subtotal = subtotal
                    });
                }

                // Gắn SaleCode = CS-{BookingCode} để liên kết chặt chẽ 2 hóa đơn cùng một giao dịch
                concessionSale = new ConcessionSale
                {
                    SaleCode = $"CS-{booking.BookingCode}",
                    StaffId = staffId,
                    SaleDate = now,
                    TotalAmount = concessionTotal,
                    PaymentMethod = paymentMethod,
                    Status = "Completed",
                    ConcessionSaleItems = saleItems
                };
                db.ConcessionSales.Add(concessionSale);
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new CounterSaleResult(booking, concessionSale);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
