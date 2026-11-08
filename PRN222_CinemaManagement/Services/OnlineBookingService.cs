using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Hubs;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using QRCoder;

namespace PRN222_CinemaManagement.Services;

// Controller xử lý HTTP/Session; service này xử lý database và nghiệp vụ đặt vé.
// Dùng constructor injection giống AccountController, không tạo thêm các lớp trung gian.
public class OnlineBookingService(CinemaDbContext context, IHubContext<SeatHub> hubContext,
    ILogger<OnlineBookingService> logger) : IOnlineBookingService
{
    // 1. XEM PHIM VÀ SƠ ĐỒ GHẾ

    public async Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default)
    {
        var movies = await context.Movies.AsNoTracking()
            .Where(m => m.Status == "Showing").OrderBy(m => m.Title).ToListAsync(ct);
        var result = new List<MovieCardViewModel>();
        foreach (var movie in movies)
        {
            result.Add(CreateMovieViewModel(movie));
        }
        return result;
    }

    public async Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default)
    {
        var movie = await context.Movies.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MovieId == movieId && m.Status == "Showing", ct);
        if (movie == null) return null;

        var now = DateTime.UtcNow;
        var showtimes = await context.Showtimes.AsNoTracking()
            .Include(s => s.Auditorium)
            .Where(s => s.MovieId == movieId && s.Status == "Scheduled"
                && s.StartAt > now && s.Auditorium.Status == "Active")
            .OrderBy(s => s.StartAt).ToListAsync(ct);

        var model = new MovieDetailsViewModel { Movie = CreateMovieViewModel(movie) };
        foreach (var showtime in showtimes)
        {
            model.Showtimes.Add(new ShowtimeOptionViewModel
            {
                ShowtimeId = showtime.ShowtimeId,
                Auditorium = showtime.Auditorium.Name,
                StartAt = AsUtc(showtime.StartAt),
                BasePrice = showtime.BasePrice
            });
        }
        return model;
    }

    public async Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default)
    {
        var showtime = await GetOpenShowtimeAsync(showtimeId, ct);
        if (showtime == null) return null;

        var now = DateTime.UtcNow;
        Booking? myBooking = null;
        if (userId.HasValue)
        {
            // Chỉ trả mã đơn của người xem, không gửi người giữ ghế khác ra trình duyệt.
            myBooking = await context.Bookings.AsNoTracking()
                .Where(b => b.ShowtimeId == showtimeId && b.BookingChannel == "Online"
                    && b.Status == "Pending" && b.ExpiresAt > now && b.Customer != null
                    && b.Customer.UserId == userId.Value && b.Customer.User.Status == "Active"
                    && b.Customer.User.Role.RoleName == "Customer")
                .OrderByDescending(b => b.BookingId).FirstOrDefaultAsync(ct);
        }

        var showtimeSeats = await context.ShowtimeSeats.AsNoTracking()
            .Include(s => s.Seat).ThenInclude(s => s.SeatType)
            .Where(s => s.ShowtimeId == showtimeId)
            .OrderBy(s => s.Seat.RowLabel).ThenBy(s => s.Seat.SeatNumber).ToListAsync(ct);

        var model = new SeatMapViewModel
        {
            ShowtimeId = showtimeId,
            MovieTitle = showtime.Movie.Title,
            Auditorium = showtime.Auditorium.Name,
            StartAt = AsUtc(showtime.StartAt),
            ServerNow = now
        };
        if (myBooking != null)
        {
            model.BookingId = myBooking.BookingId;
            model.ExpiresAt = AsUtc(myBooking.ExpiresAt);
        }

        foreach (var seat in showtimeSeats)
        {
            var status = seat.Status;
            if (status != "Sold")
            {
                if (seat.Seat.Status != "Active" || !seat.Seat.SeatType.IsActive)
                    status = "Blocked";
                else if (status == "Held" && seat.HoldExpiresAt <= now)
                    status = "Available"; // Hết hạn thì có thể chọn lại ngay, không cần chờ worker.
            }
            model.Seats.Add(new SeatViewModel
            {
                ShowtimeSeatId = seat.ShowtimeSeatId,
                RowLabel = seat.Seat.RowLabel,
                SeatNumber = seat.Seat.SeatNumber,
                SeatType = seat.Seat.SeatType.TypeName,
                Price = seat.Price,
                Status = status,
                IsMine = myBooking != null && status == "Held"
                    && seat.HeldByBookingId == myBooking.BookingId
            });
        }
        return model;
    }

    // 2. CHỌN GHẾ — tất cả ghế cùng được giữ, hoặc không thay đổi ghế nào.

    public async Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default)
    {
        var customerId = await GetActiveCustomerIdAsync(userId, ct);
        var seatIds = request.ShowtimeSeatIds;
        if (request.ShowtimeId <= 0 || request.BookingId <= 0 || seatIds == null
            || seatIds.Length > 20 || seatIds.Any(id => id <= 0)
            || seatIds.Distinct().Count() != seatIds.Length)
        {
            throw new BookingException("Danh sách ghế không hợp lệ. Mỗi đơn chọn tối đa 20 ghế.", 400);
        }

        if (seatIds.Length == 0)
        {
            if (request.BookingId.HasValue)
                await CancelAsync(userId, request.BookingId.Value, ct);
            return new HoldResultViewModel { ServerNow = DateTime.UtcNow };
        }

        var showtime = await GetOpenShowtimeAsync(request.ShowtimeId, ct);
        if (showtime == null) throw new BookingException("Suất chiếu không còn mở bán.", 404);

        // Transaction đảm bảo không giữ dở một phần ghế nếu một ghế khác đã bị mua.
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        HoldResultViewModel result;
        try
        {
            Booking booking;
            if (request.BookingId.HasValue)
            {
                booking = await LockOwnedBookingAsync(customerId, request.BookingId.Value, ct);
                if (booking.ShowtimeId != request.ShowtimeId)
                    throw new BookingException("Đơn không thuộc suất chiếu này.", 400);
                CheckPendingBooking(booking);
            }
            else
            {
                // SQL lưu chính xác tới giây. Bỏ phần lẻ để không làm tròn tăng hạn giữ.
                var now = DateTime.UtcNow;
                now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
                var expiresAt = now.AddMinutes(5);
                if (expiresAt > showtime.StartAt) expiresAt = AsUtc(showtime.StartAt);
                if (expiresAt <= DateTime.UtcNow)
                    throw new BookingException("Suất chiếu đã bắt đầu. Vui lòng chọn suất khác.");

                booking = new Booking
                {
                    BookingCode = "BK-" + Guid.NewGuid().ToString("N"),
                    CustomerId = customerId,
                    ShowtimeId = request.ShowtimeId,
                    BookingDate = now,
                    ExpiresAt = expiresAt,
                    BookingChannel = "Online",
                    Status = "Pending"
                };
                context.Bookings.Add(booking);
                await context.SaveChangesAsync(ct);
                // Các bước sau dùng ExecuteUpdate nên không để EF ghi lại bản đơn cũ.
                context.Entry(booking).State = EntityState.Detached;
            }

            var selectedSeats = await context.ShowtimeSeats.AsNoTracking()
                .Where(s => s.ShowtimeId == request.ShowtimeId && seatIds.Contains(s.ShowtimeSeatId)
                    && s.Seat.Status == "Active" && s.Seat.SeatType.IsActive)
                .OrderBy(s => s.ShowtimeSeatId).ToListAsync(ct);
            if (selectedSeats.Count != seatIds.Length)
                throw new BookingException("Có ghế không thuộc suất chiếu hoặc đang bảo trì.", 400);

            // Giữ ghế theo ID tăng dần để các request khóa ghế theo cùng thứ tự.
            foreach (var seat in selectedSeats)
            {
                var now = DateTime.UtcNow;
                var updated = await context.ShowtimeSeats
                    .Where(s => s.ShowtimeSeatId == seat.ShowtimeSeatId
                        && s.Seat.Status == "Active" && s.Seat.SeatType.IsActive
                        && (s.Status == "Available" || (s.Status == "Held"
                            && (s.HoldExpiresAt <= now || s.HeldByBookingId == booking.BookingId))))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(s => s.Status, "Held")
                        .SetProperty(s => s.HeldByBookingId, (int?)booking.BookingId)
                        .SetProperty(s => s.HoldExpiresAt, (DateTime?)booking.ExpiresAt), ct);

                // Điều kiện và UPDATE chạy cùng lúc ở SQL. Hai người không thể cùng lấy ghế.
                if (updated != 1)
                    throw new BookingException("Ghế vừa được người khác giữ hoặc mua. Vui lòng chọn lại.");
            }

            // Bỏ chọn ghế nào thì nhả ghế đó, không thay đổi hạn của các ghế giữ lại.
            await context.ShowtimeSeats
                .Where(s => s.HeldByBookingId == booking.BookingId && s.Status == "Held"
                    && !seatIds.Contains(s.ShowtimeSeatId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Status, "Available")
                    .SetProperty(s => s.HeldByBookingId, (int?)null)
                    .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);

            var total = selectedSeats.Sum(s => s.Price);
            var checkTime = DateTime.UtcNow;
            var saved = await context.Bookings
                .Where(b => b.BookingId == booking.BookingId && b.Status == "Pending" && b.ExpiresAt > checkTime
                    && b.Showtime.Status == "Scheduled" && b.Showtime.StartAt > checkTime
                    && b.Showtime.Movie.Status == "Showing" && b.Showtime.Auditorium.Status == "Active")
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.TotalAmount, total), ct);
            if (saved != 1) throw new BookingException("Đã hết thời gian giữ ghế hoặc suất chiếu đã đóng.");

            await transaction.CommitAsync(ct);
            result = new HoldResultViewModel
            {
                BookingId = booking.BookingId,
                ExpiresAt = AsUtc(booking.ExpiresAt),
                ServerNow = DateTime.UtcNow,
                TotalAmount = total
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
        // Chỉ thông báo sau khi dữ liệu được lưu thành công.
        await NotifySeatsChangedAsync(request.ShowtimeId);
        return result;
    }

    // 3. THANH TOÁN GIẢ LẬP — không gọi cổng thanh toán thật.

    public async Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default)
    {
        var customerId = await GetActiveCustomerIdAsync(userId, ct);
        if (request.Outcome != "Success" && request.Outcome != "Failed" && request.Outcome != "Cancelled")
            throw new BookingException("Kết quả thanh toán không hợp lệ.", 400);
        if (request.Method != "VNPay_Simulated" && request.Method != "MoMo_Simulated")
            throw new BookingException("Phương thức thanh toán không hợp lệ.", 400);

        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        Booking booking;
        var resultStatus = "Pending";
        try
        {
            booking = await LockOwnedBookingAsync(customerId, request.BookingId, ct);
            if (booking.Status == "Confirmed")
            {
                // Bấm lần nữa chỉ trả kết quả cũ, không tạo thêm Payment hoặc Ticket.
                await transaction.CommitAsync(ct);
                return "Confirmed";
            }
            CheckPendingBooking(booking);

            if (request.Outcome == "Cancelled")
            {
                await context.Bookings.Where(b => b.BookingId == booking.BookingId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Cancelled"), ct);
                await ReleaseHeldSeatsAsync(booking.BookingId, ct);
                resultStatus = "Cancelled";
            }
            else
            {
                var showtime = await GetOpenShowtimeAsync(booking.ShowtimeId, ct);
                if (showtime == null) throw new BookingException("Suất chiếu không còn mở bán.");

                var heldSeats = await context.ShowtimeSeats.AsNoTracking()
                    .Where(s => s.HeldByBookingId == booking.BookingId && s.Status == "Held")
                    .OrderBy(s => s.ShowtimeSeatId).ToListAsync(ct);
                if (heldSeats.Count == 0 || heldSeats.Any(s => s.HoldExpiresAt <= DateTime.UtcNow)
                    || heldSeats.Sum(s => s.Price) != booking.TotalAmount)
                    throw new BookingException("Ghế hoặc giá vé đã thay đổi. Vui lòng chọn lại.");

                if (request.Outcome == "Success")
                {
                    foreach (var seat in heldSeats)
                    {
                        var now = DateTime.UtcNow;
                        var sold = await context.ShowtimeSeats
                            .Where(s => s.ShowtimeSeatId == seat.ShowtimeSeatId && s.Status == "Held"
                                && s.HeldByBookingId == booking.BookingId && s.HoldExpiresAt > now
                                && s.Seat.Status == "Active" && s.Seat.SeatType.IsActive)
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(s => s.Status, "Sold")
                                .SetProperty(s => s.HeldByBookingId, (int?)null)
                                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
                        if (sold != 1) throw new BookingException("Ghế không còn được giữ cho đơn này.");

                        context.Tickets.Add(new Ticket
                        {
                            TicketCode = "TK-" + Guid.NewGuid().ToString("N"),
                            BookingId = booking.BookingId,
                            ShowtimeId = booking.ShowtimeId,
                            ShowtimeSeatId = seat.ShowtimeSeatId,
                            Price = seat.Price,
                            Status = "Valid",
                            IssuedAt = now
                        });
                    }

                    // Kiểm tra lại sau khi đợi khóa, tránh phát vé khi hạn giữ vừa kết thúc.
                    var checkTime = DateTime.UtcNow;
                    var confirmed = await context.Bookings
                        .Where(b => b.BookingId == booking.BookingId && b.Status == "Pending" && b.ExpiresAt > checkTime
                            && b.Showtime.Status == "Scheduled" && b.Showtime.StartAt > checkTime
                            && b.Showtime.Movie.Status == "Showing" && b.Showtime.Auditorium.Status == "Active")
                        .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Confirmed"), ct);
                    if (confirmed != 1) throw new BookingException("Hết thời gian thanh toán hoặc suất chiếu đã đóng.");
                    resultStatus = "Confirmed";
                }
                // Nếu Failed, giữ nguyên Pending và hạn giữ để khách thử lại.
            }

            var payment = new Payment
            {
                BookingId = booking.BookingId,
                PaymentCode = "PM-" + Guid.NewGuid().ToString("N"),
                Provider = request.Method,
                TransactionRef = Guid.NewGuid().ToString("N"),
                Amount = booking.TotalAmount,
                PaymentMethod = request.Method,
                PaymentStatus = request.Outcome,
                CreatedAt = DateTime.UtcNow
            };
            if (request.Outcome == "Success") payment.PaidAt = DateTime.UtcNow;
            context.Payments.Add(payment);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Đơn, ghế, thanh toán và vé phải cùng rollback nếu một bước thất bại.
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }

        await NotifySeatsChangedAsync(booking.ShowtimeId);
        if (resultStatus == "Confirmed")
        {
            // Email chỉ giả lập qua log theo spec; khách xem QR tại trang Vé của tôi.
            logger.LogInformation("Email giả lập: đơn {BookingCode} đã thanh toán và có vé điện tử.", booking.BookingCode);
        }
        return resultStatus;
    }

    // 4. HỦY ĐƠN VÀ GIẢI PHÓNG GHẾ HẾT HẠN

    public async Task CancelAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await GetActiveCustomerIdAsync(userId, ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        Booking booking;
        try
        {
            booking = await LockOwnedBookingAsync(customerId, bookingId, ct);
            if (booking.Status != "Pending" && booking.Status != "Cancelled" && booking.Status != "Expired")
                throw new BookingException("Không thể hủy đơn đã thanh toán.");

            await context.Bookings.Where(b => b.BookingId == bookingId && b.Status == "Pending")
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Cancelled"), ct);
            await ReleaseHeldSeatsAsync(bookingId, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        await NotifySeatsChangedAsync(booking.ShowtimeId);
    }

    public async Task<int> ReleaseExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredBookings = await context.Bookings.AsNoTracking()
            .Where(b => b.Status == "Pending" && b.ExpiresAt <= now)
            .OrderBy(b => b.BookingId).Take(200).ToListAsync(ct);

        var count = 0;
        foreach (var booking in expiredBookings)
        {
            // Mỗi đơn một transaction. Worker khóa đơn trước, ghế sau như thanh toán/hủy.
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var changed = await context.Bookings
                .Where(b => b.BookingId == booking.BookingId && b.Status == "Pending" && b.ExpiresAt <= now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Expired"), ct);
            if (changed == 1)
            {
                await ReleaseHeldSeatsAsync(booking.BookingId, ct);
                count++;
            }
            await transaction.CommitAsync(ct);
            if (changed == 1) await NotifySeatsChangedAsync(booking.ShowtimeId);
        }
        return count;
    }

    // 5. XEM ĐƠN VÀ VÉ — luôn kiểm tra chủ sở hữu tại server.

    public async Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var customerId = await GetActiveCustomerIdAsync(userId, ct);
        var booking = await GetBookingDetailsQuery(customerId)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);
        if (booking == null) return null;
        return CreateBookingViewModel(booking);
    }

    public async Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default)
    {
        var customerId = await GetActiveCustomerIdAsync(userId, ct);
        var bookings = await GetBookingDetailsQuery(customerId)
            .OrderByDescending(b => b.Showtime.StartAt).ThenByDescending(b => b.BookingId).ToListAsync(ct);
        var result = new List<BookingDetailsViewModel>();
        foreach (var booking in bookings)
        {
            result.Add(CreateBookingViewModel(booking));
        }
        return result;
    }

    public async Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default)
    {
        var customerId = await GetActiveCustomerIdAsync(userId, ct);
        var ticket = await context.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.Booking.CustomerId == customerId
                && t.Booking.BookingChannel == "Online" && t.Booking.Status == "Confirmed" && t.Status != "Cancelled", ct);
        if (ticket == null) return null;

        // QRCoder tạo ảnh PNG trực tiếp từ mã vé, không cần thêm interface/lớp bọc.
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticket.TicketCode, QRCodeGenerator.ECCLevel.Q);
        using var image = new PngByteQRCode(data);
        return image.GetGraphic(8);
    }

    // CÁC HÀM NHỎ DÙNG CHUNG — chỉ tách những đoạn được gọi nhiều lần.

    private async Task<int> GetActiveCustomerIdAsync(int userId, CancellationToken ct)
    {
        // Session có thể còn hiệu lực sau khi tài khoản bị khóa, vì vậy cần kiểm tra DB.
        var customer = await context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.User.Status == "Active"
                && c.User.Role.RoleName == "Customer", ct);
        if (customer == null) throw new BookingException("Tài khoản khách hàng không hợp lệ hoặc đã bị khóa.", 403);
        return customer.CustomerId;
    }

    private async Task<Showtime?> GetOpenShowtimeAsync(int showtimeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await context.Showtimes.AsNoTracking()
            .Include(s => s.Movie).Include(s => s.Auditorium)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId && s.Status == "Scheduled"
                && s.StartAt > now && s.Movie.Status == "Showing" && s.Auditorium.Status == "Active", ct);
    }

    private async Task<Booking> LockOwnedBookingAsync(int customerId, int bookingId, CancellationToken ct)
    {
        // Phải gọi trong transaction. UPDATE giữ nguyên Pending nhưng khóa dòng đơn đến commit.
        // Request thứ hai trên cùng đơn phải chờ, sau đó đọc lại trạng thái mới nhất.
        await context.Bookings
            .Where(b => b.BookingId == bookingId && b.CustomerId == customerId
                && b.BookingChannel == "Online" && b.Status == "Pending")
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, "Pending"), ct);

        var booking = await context.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId
                && b.BookingChannel == "Online", ct);
        if (booking == null) throw new BookingException("Không tìm thấy đơn đặt vé của bạn.", 404);
        return booking;
    }

    private static void CheckPendingBooking(Booking booking)
    {
        if (booking.Status != "Pending" || booking.ExpiresAt <= DateTime.UtcNow)
            throw new BookingException("Đơn đã hết hạn hoặc không còn chờ thanh toán. Vui lòng chọn ghế lại.");
    }

    private async Task ReleaseHeldSeatsAsync(int bookingId, CancellationToken ct)
    {
        // Không nhả ghế Sold hoặc ghế hết hạn đã được đơn khác giữ lại.
        await context.ShowtimeSeats
            .Where(s => s.HeldByBookingId == bookingId && s.Status == "Held")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, "Available")
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }

    private async Task NotifySeatsChangedAsync(int showtimeId)
    {
        try
        {
            await hubContext.Clients.Group(SeatHub.GroupName(showtimeId))
                .SendAsync("SeatStatusChanged", new { showtimeId });
        }
        catch (Exception ex)
        {
            // DB đã commit: không báo thanh toán lỗi chỉ vì SignalR mất kết nối.
            // Trình duyệt vẫn có thể tải lại sơ đồ bằng HTTP.
            logger.LogWarning(ex, "Không gửi được cập nhật sơ đồ suất {ShowtimeId}.", showtimeId);
        }
    }

    private IQueryable<Booking> GetBookingDetailsQuery(int customerId)
    {
        // Dùng chung Include cho trang chi tiết và danh sách; Where vẫn luôn lọc chủ đơn.
        return context.Bookings.AsNoTracking().AsSplitQuery()
            .Where(b => b.CustomerId == customerId && b.BookingChannel == "Online")
            .Include(b => b.Customer).ThenInclude(c => c!.User)
            .Include(b => b.Showtime).ThenInclude(s => s.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s.Auditorium)
            .Include(b => b.HeldShowtimeSeats).ThenInclude(s => s.Seat).ThenInclude(s => s.SeatType)
            .Include(b => b.Tickets).ThenInclude(t => t.ShowtimeSeat).ThenInclude(s => s.Seat).ThenInclude(s => s.SeatType);
    }

    private static MovieCardViewModel CreateMovieViewModel(Movie movie)
    {
        return new MovieCardViewModel
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            Description = movie.Description,
            DurationMinutes = movie.DurationMinutes,
            AgeRating = movie.AgeRating,
            PosterUrl = movie.PosterUrl
        };
    }

    private static BookingDetailsViewModel CreateBookingViewModel(Booking booking)
    {
        var model = new BookingDetailsViewModel
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            ShowtimeId = booking.ShowtimeId,
            MovieTitle = booking.Showtime.Movie.Title,
            Auditorium = booking.Showtime.Auditorium.Name,
            StartAt = AsUtc(booking.Showtime.StartAt),
            Status = booking.Status,
            ExpiresAt = AsUtc(booking.ExpiresAt),
            ServerNow = DateTime.UtcNow,
            TotalAmount = booking.TotalAmount,
            CustomerName = booking.Customer!.User.FullName,
            Email = booking.Customer.User.Email
        };
        if (booking.Status == "Pending" && booking.ExpiresAt <= model.ServerNow)
            model.Status = "Expired";

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
        return model;
    }

    // confirm payment callback from VNPay
    // confirm payment callback from VNPay
    public async Task<string> ConfirmPaymentAsync(int bookingId, bool isSuccess, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        Booking? booking;
        var resultStatus = "Failed";

        try
        {
            // 1. Lấy thông tin đơn hàng
            booking = await context.Bookings
                .Include(b => b.HeldShowtimeSeats)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);

            if (booking == null) throw new BookingException("Không tìm thấy đơn hàng", 404);

            // Nếu đơn đã Confirmed rồi thì trả về luôn (chống F5)
            if (booking.Status == "Confirmed")
            {
                await transaction.CommitAsync(ct);
                return "Confirmed";
            }

            if (booking.Status != "Pending" || booking.ExpiresAt <= DateTime.UtcNow)
            {
                throw new BookingException("Đơn đã hết hạn hoặc không còn chờ thanh toán.");
            }

            if (isSuccess)
            {
                // === THANH TOÁN THÀNH CÔNG ===
                var now = DateTime.UtcNow;
                var heldSeats = booking.HeldShowtimeSeats.Where(s => s.Status == "Held").ToList();

                if (heldSeats.Count == 0)
                    throw new BookingException("Không tìm thấy ghế đang giữ cho đơn này.");

                foreach (var seat in heldSeats)
                {
                    // Cập nhật ghế thành Sold
                    var sold = await context.ShowtimeSeats
                        .Where(s => s.ShowtimeSeatId == seat.ShowtimeSeatId && s.Status == "Held" && s.HeldByBookingId == booking.BookingId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(s => s.Status, "Sold")
                            .SetProperty(s => s.HeldByBookingId, (int?)null)
                            .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);

                    if (sold != 1) throw new BookingException("Ghế không còn được giữ cho đơn này.");

                    // Tạo vé
                    context.Tickets.Add(new Ticket
                    {
                        TicketCode = "TK-" + Guid.NewGuid().ToString("N"),
                        BookingId = booking.BookingId,
                        ShowtimeId = booking.ShowtimeId,
                        ShowtimeSeatId = seat.ShowtimeSeatId,
                        Price = seat.Price,
                        Status = "Valid",
                        IssuedAt = now
                    });
                }

                // Cập nhật trạng thái đơn
                booking.Status = "Confirmed";

                // Lưu lịch sử thanh toán
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    PaymentCode = "PM-" + Guid.NewGuid().ToString("N"),
                    Provider = "VNPay",
                    TransactionRef = Guid.NewGuid().ToString("N"), // Có thể lưu vnp_TransactionNo từ VNPay nếu muốn
                    Amount = booking.TotalAmount,
                    PaymentMethod = "VNPay",
                    PaymentStatus = "Success",
                    CreatedAt = now,
                    PaidAt = now
                };
                context.Payments.Add(payment);

                resultStatus = "Confirmed";
            }
            else
            {
                // Thanh toán thất bại, giữ nguyên Pending để khách thử lại hoặc cho Background Worker quét
                resultStatus = "Failed";
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }

        // Báo SignalR cập nhật lại sơ đồ ghế
        if (resultStatus == "Confirmed" && booking != null)
        {
            await NotifySeatsChangedAsync(booking.ShowtimeId);
        }

        return resultStatus;
    }

    private static DateTime AsUtc(DateTime value)
    {
        // SQL datetime2 không lưu múi giờ; gắn UTC để JSON có hậu tố Z và JS đếm đúng giờ.
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
