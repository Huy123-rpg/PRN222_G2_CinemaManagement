using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

/// <summary>
/// EF Core implementation of <see cref="ICounterTicketRepository"/>.
/// Chuẩn Lab 2 PRN222: Truy vấn LINQ đơn giản, thực thi Database Transaction nguyên tử.
/// </summary>
public class CounterTicketRepository(CinemaDbContext db) : ICounterTicketRepository
{
    public Task<List<Movie>> GetShowingMoviesAsync(string? statusFilter = null)
    {
        var query = db.Movies.AsNoTracking();

        if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Showing, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => m.Status == BookingConstants.MovieStatuses.Showing);
        }
        else if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Upcoming, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(statusFilter, "Presale", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => m.Status == BookingConstants.MovieStatuses.Upcoming);
        }
        else
        {
            query = query.Where(m => m.Status == BookingConstants.MovieStatuses.Showing
                                  || m.Status == BookingConstants.MovieStatuses.Upcoming);
        }

        return query.OrderBy(m => m.Title).ToListAsync();
    }

    public Task<List<Screening>> GetAvailableScreeningsAsync(int? movieId, string? statusFilter = null)
    {
        var now = DateTime.UtcNow;
        var query = db.Screenings
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .Where(s => s.Status == BookingConstants.ScreeningStatuses.Scheduled
                     && s.StartAt > now
                     && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active);

        if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Showing, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Movie.Status == BookingConstants.MovieStatuses.Showing);
        }
        else if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Upcoming, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(statusFilter, "Presale", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Movie.Status == BookingConstants.MovieStatuses.Upcoming);
        }
        else
        {
            query = query.Where(s => s.Movie.Status == BookingConstants.MovieStatuses.Showing
                                  || s.Movie.Status == BookingConstants.MovieStatuses.Upcoming);
        }

        if (movieId.HasValue && movieId.Value > 0)
        {
            query = query.Where(s => s.MovieId == movieId.Value);
        }

        return query.OrderBy(s => s.StartAt).ToListAsync();
    }

    public async Task<(List<Screening> Items, int TotalCount)> GetAvailableScreeningsPagedAsync(
        int? movieId, int page, int pageSize, string? statusFilter = null)
    {
        var now = DateTime.UtcNow;
        var query = db.Screenings
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .Where(s => s.Status == BookingConstants.ScreeningStatuses.Scheduled
                     && s.StartAt > now
                     && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active);

        if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Showing, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Movie.Status == BookingConstants.MovieStatuses.Showing);
        }
        else if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Upcoming, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(statusFilter, "Presale", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Movie.Status == BookingConstants.MovieStatuses.Upcoming);
        }
        else
        {
            query = query.Where(s => s.Movie.Status == BookingConstants.MovieStatuses.Showing
                                  || s.Movie.Status == BookingConstants.MovieStatuses.Upcoming);
        }

        if (movieId.HasValue && movieId.Value > 0)
        {
            query = query.Where(s => s.MovieId == movieId.Value);
        }

        int totalCount = await query.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var items = await query
            .OrderBy(s => s.StartAt)
            .ThenBy(s => s.ScreeningId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<Screening?> GetScreeningByIdAsync(int screeningId) =>
        db.Screenings
            .AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .FirstOrDefaultAsync(s => s.ScreeningId == screeningId);

    public async Task<List<ScreeningSeat>> GetScreeningSeatsAsync(int screeningId)
    {
        var now = DateTime.UtcNow;
        var seats = await db.ScreeningSeats
            .AsNoTracking()
            .Include(ss => ss.Seat).ThenInclude(s => s.SeatType)
            .Where(ss => ss.ScreeningId == screeningId)
            .OrderBy(ss => ss.Seat.RowLabel)
            .ThenBy(ss => ss.Seat.SeatNumber)
            .ToListAsync();

        // Chuẩn hóa trạng thái đồng bộ hoàn toàn với module của Ánh và Bách:
        foreach (var ss in seats)
        {
            if (ss.Status != BookingConstants.ScreeningSeatStatuses.Sold)
            {
                if (ss.Seat.Status != BookingConstants.PhysicalSeatStatuses.Active || !ss.Seat.SeatType.IsActive)
                {
                    ss.Status = BookingConstants.ScreeningSeatStatuses.Blocked;
                }
                else if (ss.Status == BookingConstants.ScreeningSeatStatuses.Held && ss.HoldExpiresAt <= now)
                {
                    // Ghế giữ online quá 5 phút đã hết hạn -> tự động hiển thị Available để quầy bán được
                    ss.Status = BookingConstants.ScreeningSeatStatuses.Available;
                }
            }
        }

        return seats;
    }

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

    public Task<Booking?> GetActivePendingCounterBookingAsync(int bookingId) =>
        db.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId
                && b.BookingChannel == BookingConstants.Channels.Counter
                && b.Status == BookingConstants.BookingStatuses.Pending
                && b.ExpiresAt > DateTime.UtcNow);

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

    public async Task<HoldResultViewModel> HoldCounterSeatsAsync(
        int staffId,
        int screeningId,
        int? bookingId,
        int[] seatIds,
        CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

            var screening = await db.Screenings
                .Include(s => s.Auditorium)
                .FirstOrDefaultAsync(s => s.ScreeningId == screeningId, ct);

            if (screening == null || screening.Status != BookingConstants.ScreeningStatuses.Scheduled || screening.StartAt <= now)
            {
                throw new InvalidOperationException("Suất chiếu không khả dụng hoặc đã qua giờ chiếu.");
            }

            Booking booking;
            if (bookingId.HasValue && bookingId.Value > 0)
            {
                var existingBooking = await db.Bookings
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId.Value
                        && b.ScreeningId == screeningId
                        && b.StaffId == staffId
                        && b.BookingChannel == BookingConstants.Channels.Counter
                        && b.Status == BookingConstants.BookingStatuses.Pending, ct);

                if (existingBooking == null)
                {
                    throw new InvalidOperationException("Phiên giữ ghế không tồn tại hoặc đã hết hạn.");
                }

                booking = existingBooking;

                if (booking.ExpiresAt <= now)
                {
                    throw new InvalidOperationException("Thời gian 5 phút giữ ghế đã hết hạn. Vui lòng chọn lại.");
                }
            }
            else
            {
                if (seatIds.Length == 0)
                {
                    return new HoldResultViewModel { ServerNow = now };
                }

                var expiresAt = now.AddMinutes(BookingConstants.HoldDurationMinutes);
                if (expiresAt > screening.StartAt) expiresAt = screening.StartAt;
                if (expiresAt <= now)
                {
                    throw new InvalidOperationException("Suất chiếu đã bắt đầu.");
                }

                booking = new Booking
                {
                    BookingCode = "CT-HOLD-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    CustomerId = null,
                    StaffId = staffId,
                    ScreeningId = screeningId,
                    BookingDate = now,
                    ExpiresAt = expiresAt,
                    BookingChannel = BookingConstants.Channels.Counter,
                    Status = BookingConstants.BookingStatuses.Pending,
                    TotalAmount = 0
                };
                db.Bookings.Add(booking);
                await db.SaveChangesAsync(ct);
            }

            if (seatIds.Length == 0)
            {
                // Nhả tất cả ghế của đơn này
                await db.ScreeningSeats
                    .Where(s => s.HeldByBookingId == booking.BookingId && s.Status == BookingConstants.ScreeningSeatStatuses.Held)
                    .ExecuteUpdateAsync(setter => setter
                        .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Available)
                        .SetProperty(s => s.HeldByBookingId, (int?)null)
                        .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);

                booking.Status = BookingConstants.BookingStatuses.Cancelled;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return new HoldResultViewModel { ServerNow = now };
            }

            var selectedSeats = await db.ScreeningSeats
                .Include(ss => ss.Seat).ThenInclude(s => s.SeatType)
                .Where(ss => seatIds.Contains(ss.ScreeningSeatId) && ss.ScreeningId == screeningId)
                .OrderBy(ss => ss.ScreeningSeatId)
                .ToListAsync(ct);

            if (selectedSeats.Count != seatIds.Length ||
                selectedSeats.Any(s => s.Seat.Status != BookingConstants.PhysicalSeatStatuses.Active || !s.Seat.SeatType.IsActive))
            {
                throw new InvalidOperationException("Có ghế không hợp lệ hoặc đang bảo trì.");
            }

            // Giữ ghế tuần tự theo ID tăng dần chống deadlock
            foreach (var seat in selectedSeats)
            {
                var updated = await db.ScreeningSeats
                    .Where(s => s.ScreeningSeatId == seat.ScreeningSeatId
                        && s.Seat.Status == BookingConstants.PhysicalSeatStatuses.Active
                        && s.Seat.SeatType.IsActive
                        && (s.Status == BookingConstants.ScreeningSeatStatuses.Available
                            || (s.Status == BookingConstants.ScreeningSeatStatuses.Held
                                && (s.HoldExpiresAt <= now || s.HeldByBookingId == booking.BookingId))))
                    .ExecuteUpdateAsync(setter => setter
                        .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Held)
                        .SetProperty(s => s.HeldByBookingId, (int?)booking.BookingId)
                        .SetProperty(s => s.HoldExpiresAt, (DateTime?)booking.ExpiresAt), ct);

                if (updated != 1)
                {
                    throw new InvalidOperationException($"Ghế {seat.Seat.RowLabel}{seat.Seat.SeatNumber} vừa được người khác giữ hoặc mua.");
                }
            }

            // Nhả các ghế Staff vừa bỏ chọn
            await db.ScreeningSeats
                .Where(s => s.HeldByBookingId == booking.BookingId
                    && s.Status == BookingConstants.ScreeningSeatStatuses.Held
                    && !seatIds.Contains(s.ScreeningSeatId))
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Available)
                    .SetProperty(s => s.HeldByBookingId, (int?)null)
                    .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);

            booking.TotalAmount = selectedSeats.Sum(s => s.Price);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new HoldResultViewModel
            {
                BookingId = booking.BookingId,
                ExpiresAt = booking.ExpiresAt,
                ServerNow = now,
                TotalAmount = booking.TotalAmount
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ReleaseCounterSeatsAsync(int staffId, int bookingId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var booking = await db.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId
                    && b.StaffId == staffId
                    && b.BookingChannel == BookingConstants.Channels.Counter
                    && b.Status == BookingConstants.BookingStatuses.Pending, ct);

            if (booking != null)
            {
                await db.ScreeningSeats
                    .Where(s => s.HeldByBookingId == booking.BookingId && s.Status == BookingConstants.ScreeningSeatStatuses.Held)
                    .ExecuteUpdateAsync(setter => setter
                        .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Available)
                        .SetProperty(s => s.HeldByBookingId, (int?)null)
                        .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);

                booking.Status = BookingConstants.BookingStatuses.Cancelled;
                await db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<CounterSaleResult> ExecuteCounterSaleTransactionAsync(
        int screeningId,
        List<int> seatIds,
        List<CounterConcessionOrder>? concessionOrders,
        int staffId,
        decimal ticketTotal,
        string paymentMethod,
        int? holdingBookingId = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;

            // 1. Cập nhật nguyên tử trạng thái ghế từ Available hoặc Held -> Sold
            // Ràng buộc CK_ScreeningSeats_HeldState yêu cầu HeldByBookingId và HoldExpiresAt phải là NULL khi Status != 'Held'
            var updatedRows = await db.ScreeningSeats
                .Where(ss => seatIds.Contains(ss.ScreeningSeatId)
                          && ss.ScreeningId == screeningId
                          && ss.Seat.Status == BookingConstants.PhysicalSeatStatuses.Active
                          && ss.Seat.SeatType.IsActive
                          && (ss.Status == BookingConstants.ScreeningSeatStatuses.Available
                              || (ss.Status == BookingConstants.ScreeningSeatStatuses.Held
                                  && (ss.HoldExpiresAt <= now
                                      || (holdingBookingId.HasValue && ss.HeldByBookingId == holdingBookingId.Value)))))
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(s => s.Status, BookingConstants.ScreeningSeatStatuses.Sold)
                    .SetProperty(s => s.HeldByBookingId, (int?)null)
                    .SetProperty(s => s.HoldExpiresAt, (DateTime?)null));

            if (updatedRows != seatIds.Count)
            {
                throw new InvalidOperationException(
                    "Giao dịch không thành công: một trong các ghế bạn chọn vừa được mua hoặc giữ bởi người khác. Vui lòng chọn ghế khác.");
            }

            // 2. Tạo hoặc cập nhật hóa đơn vé (Booking)
            Booking booking;
            if (holdingBookingId.HasValue && holdingBookingId.Value > 0)
            {
                var existing = await db.Bookings
                    .FirstOrDefaultAsync(b => b.BookingId == holdingBookingId.Value
                        && b.StaffId == staffId
                        && b.BookingChannel == BookingConstants.Channels.Counter
                        && b.Status == BookingConstants.BookingStatuses.Pending);

                if (existing != null)
                {
                    existing.BookingCode = "CT-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                    existing.TotalAmount = ticketTotal;
                    existing.Status = BookingConstants.BookingStatuses.Confirmed;
                    existing.ExpiresAt = now.AddDays(1);
                    booking = existing;
                }
                else
                {
                    booking = new Booking
                    {
                        BookingCode = "CT-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                        CustomerId = null,
                        StaffId = staffId,
                        ScreeningId = screeningId,
                        BookingDate = now,
                        ExpiresAt = now.AddDays(1),
                        TotalAmount = ticketTotal,
                        BookingChannel = BookingConstants.Channels.Counter,
                        Status = BookingConstants.BookingStatuses.Confirmed
                    };
                    db.Bookings.Add(booking);
                }
            }
            else
            {
                booking = new Booking
                {
                    BookingCode = "CT-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    CustomerId = null,
                    StaffId = staffId,
                    ScreeningId = screeningId,
                    BookingDate = now,
                    ExpiresAt = now.AddDays(1),
                    TotalAmount = ticketTotal,
                    BookingChannel = BookingConstants.Channels.Counter,
                    Status = BookingConstants.BookingStatuses.Confirmed
                };
                db.Bookings.Add(booking);
            }

            await db.SaveChangesAsync(); // Sinh BookingId hoặc lưu cập nhật

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
                    throw new InvalidOperationException("Có sản phẩm bắp nước không tồn tại hoặc đã ngừng kinh doanh.");
                }

                decimal concessionTotal = 0m;
                var saleItems = new List<ConcessionSaleItem>();

                foreach (var order in concessionOrders)
                {
                    var dbItem = dbItems.First(ci => ci.ConcessionItemId == order.ItemId);
                    if (dbItem.StockQuantity < order.Quantity)
                    {
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

    public async Task<(List<Movie> Items, int TotalCount, int ShowingCount, int UpcomingCount)> GetCounterCatalogMoviesPagedAsync(
        string? search, string? statusFilter, int page, int pageSize)
    {
        var now = DateTime.UtcNow;
        var baseQuery = db.Movies.AsNoTracking()
            .Where(m => (m.Status == BookingConstants.MovieStatuses.Showing || m.Status == BookingConstants.MovieStatuses.Upcoming)
                     && m.Screenings.Any(s => s.Status == BookingConstants.ScreeningStatuses.Scheduled
                                          && s.StartAt > now
                                          && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active));

        int showingCount = await baseQuery.CountAsync(m => m.Status == BookingConstants.MovieStatuses.Showing);
        int upcomingCount = await baseQuery.CountAsync(m => m.Status == BookingConstants.MovieStatuses.Upcoming);

        var filteredQuery = baseQuery;

        if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Showing, StringComparison.OrdinalIgnoreCase))
        {
            filteredQuery = filteredQuery.Where(m => m.Status == BookingConstants.MovieStatuses.Showing);
        }
        else if (string.Equals(statusFilter, BookingConstants.MovieStatuses.Upcoming, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(statusFilter, "Presale", StringComparison.OrdinalIgnoreCase))
        {
            filteredQuery = filteredQuery.Where(m => m.Status == BookingConstants.MovieStatuses.Upcoming);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            filteredQuery = filteredQuery.Where(m => m.Title.ToLower().Contains(term)
                                                  || (m.Description != null && m.Description.ToLower().Contains(term)));
        }

        int totalCount = await filteredQuery.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var items = await filteredQuery
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Include(m => m.Screenings.Where(s => s.Status == BookingConstants.ScreeningStatuses.Scheduled
                                              && s.StartAt > now
                                              && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active))
            .OrderBy(m => m.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount, showingCount, upcomingCount);
    }

    public async Task<Movie?> GetMovieWithScreeningsForCounterAsync(int movieId, DateOnly? showDate)
    {
        var now = DateTime.UtcNow;
        var movie = await db.Movies.AsNoTracking()
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Include(m => m.Screenings.Where(s => s.Status == BookingConstants.ScreeningStatuses.Scheduled
                                              && s.StartAt > now
                                              && s.Auditorium.Status == BookingConstants.AuditoriumStatuses.Active))
                .ThenInclude(s => s.Auditorium)
            .FirstOrDefaultAsync(m => m.MovieId == movieId);

        if (movie == null) return null;

        if (showDate.HasValue)
        {
            movie.Screenings = movie.Screenings
                .Where(s => DateOnly.FromDateTime(s.StartAt.AddHours(BookingConstants.VietnamUtcOffsetHours)) == showDate.Value)
                .OrderBy(s => s.StartAt)
                .ToList();
        }
        else
        {
            movie.Screenings = movie.Screenings
                .OrderBy(s => s.StartAt)
                .ToList();
        }

        return movie;
    }

    public async Task<(List<Booking> Items, int TotalCount, decimal TotalRevenue)> GetCounterSalesHistoryPagedAsync(
        int staffId, string? searchCode, DateTime? utcFrom, DateTime? utcTo, int page, int pageSize)
    {
        var query = db.Bookings.AsNoTracking()
            .Include(b => b.Payments)
            .Include(b => b.Tickets).ThenInclude(t => t.ScreeningSeat).ThenInclude(ss => ss.Seat)
            .Include(b => b.Screening).ThenInclude(sc => sc.Movie)
            .Include(b => b.Screening).ThenInclude(sc => sc.Auditorium)
            .Where(b => b.BookingChannel == BookingConstants.Channels.Counter
                     && b.StaffId == staffId
                     && b.Payments.Any(p => p.PaymentStatus == BookingConstants.PaymentOutcomes.Success));

        if (!string.IsNullOrWhiteSpace(searchCode))
        {
            string codeTerm = searchCode.Trim().ToLower();
            query = query.Where(b => b.BookingCode.ToLower().Contains(codeTerm));
        }

        if (utcFrom.HasValue)
        {
            query = query.Where(b => b.BookingDate >= utcFrom.Value);
        }

        if (utcTo.HasValue)
        {
            query = query.Where(b => b.BookingDate < utcTo.Value);
        }

        decimal totalRevenue = await query.SumAsync(b => b.TotalAmount);
        int totalCount = await query.CountAsync();

        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var items = await query
            .OrderByDescending(b => b.BookingDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount, totalRevenue);
    }

    public Task<ConcessionSale?> GetConcessionSaleByBookingCodeAsync(string bookingCode)
    {
        string linkedCode = "CS-" + bookingCode;
        return db.ConcessionSales.AsNoTracking()
            .Include(cs => cs.ConcessionSaleItems).ThenInclude(csi => csi.ConcessionItem)
            .FirstOrDefaultAsync(cs => cs.SaleCode == linkedCode);
    }
}
