using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Services.OnlineBooking;

namespace PRN222_CinemaManagement.Services;

/// <summary>
/// Tầng điều phối nghiệp vụ đặt vé online.
/// Đây là nơi duy nhất được phép ghi trạng thái ghế, tạo đơn, ghi thanh toán và phát vé.
/// Mọi thao tác đều dựa trên dữ liệu lấy từ máy chủ; không tin giá, tổng tiền hay
/// danh tính người mua do trình duyệt gửi lên.
/// </summary>
public sealed class OnlineBookingService : IOnlineBookingService
{
    private readonly CinemaDbContext db;
    private readonly TimeProvider clock;
    private readonly ISeatNotifier notifier;
    private readonly ITicketEmailSender emailSender;
    private readonly SeatHoldManager seats;
    private readonly BookingCatalogService catalog;
    private readonly IPaymentGateway gateway;
    private readonly IQrCodeGenerator qr;

    public OnlineBookingService(
        CinemaDbContext db,
        TimeProvider clock,
        ISeatNotifier notifier,
        ITicketEmailSender emailSender,
        ILogger<OnlineBookingService> logger,
        SeatHoldManager? seatManager = null,
        BookingCatalogService? catalogService = null,
        IPaymentGateway? paymentGateway = null,
        IQrCodeGenerator? qrCodeGenerator = null)
    {
        this.db = db;
        this.clock = clock;
        this.notifier = notifier;
        this.emailSender = emailSender;
        // Các tầng phụ được tạo từ DbContext khi không được tiêm vào (ví dụ trong kiểm thử).
        seats = seatManager ?? new SeatHoldManager(db);
        catalog = catalogService ?? new BookingCatalogService(db, clock);
        gateway = paymentGateway ?? new SimulatedPaymentGateway();
        qr = qrCodeGenerator ?? new QrCodeGenerator();
        Logger = logger;
    }

    private ILogger<OnlineBookingService> Logger { get; }

    // ==================================================================
    // 1. Đọc dữ liệu (không tạo đơn, không thay đổi trạng thái ghế)
    // ==================================================================

    public Task<IReadOnlyList<MovieCardViewModel>> GetMoviesAsync(CancellationToken ct = default) =>
        catalog.GetMoviesAsync(ct);

    public Task<MovieDetailsViewModel?> GetMovieAsync(int movieId, CancellationToken ct = default) =>
        catalog.GetMovieAsync(movieId, ct);

    public Task<SeatMapViewModel?> GetSeatMapAsync(int showtimeId, int? userId, CancellationToken ct = default) =>
        catalog.GetSeatMapAsync(showtimeId, userId, ct);

    public Task<BookingDetailsViewModel?> GetBookingAsync(int userId, int bookingId, CancellationToken ct = default) =>
        catalog.GetBookingAsync(userId, bookingId, ct);

    public Task<IReadOnlyList<BookingDetailsViewModel>> GetMyBookingsAsync(int userId, CancellationToken ct = default) =>
        catalog.GetMyBookingsAsync(userId, ct);

    // ==================================================================
    // 2. Giữ / nhả ghế (TEC-02 - chống bán trùng ghế)
    // ==================================================================

    /// <summary>
    /// Giữ (hoặc thay đổi) lựa chọn ghế của khách.
    /// Toàn bộ thao tác nằm trong một transaction: nếu một ghế trong nhóm không còn khả dụng
    /// thì mọi ghế đã giữ trong nhóm cũng được hoàn tác, không để lại đơn mồ côi.
    /// </summary>
    public async Task<HoldResultViewModel> SetSeatsAsync(int userId, SetSeatsRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await EnsureActiveCustomerAsync(userId, ct);

        var desiredIds = (request.ShowtimeSeatIds ?? [])
            .Distinct()
            .OrderBy(id => id) // Sắp xếp tăng dần để tránh deadlock khi hai khách chọn ghế chéo nhau.
            .ToArray();

        if (desiredIds.Length > BookingStates.MaxSeatsPerBooking)
            throw new BookingException($"Mỗi đơn chỉ được chọn tối đa {BookingStates.MaxSeatsPerBooking} ghế.", 400);

        // Suất chiếu phải còn mở bán: đã lịch, chưa bắt đầu, phim đang chiếu, phòng hoạt động.
        var showtime = await db.Showtimes
            .AsNoTracking()
            .Where(s => s.ShowtimeId == request.ShowtimeId
                     && s.Status == "Scheduled"
                     && s.StartAt > now
                     && s.Movie.Status == "Showing"
                     && s.Auditorium.Status == "Active")
            .Select(s => new { s.ShowtimeId, s.StartAt })
            .FirstOrDefaultAsync(ct);

        if (showtime is null)
            throw new BookingException("Suất chiếu không tồn tại hoặc đã ngừng nhận đặt vé.", 404);

        // Lựa chọn rỗng kèm đơn đang có = yêu cầu bỏ chọn tất cả và nhả ghế.
        if (desiredIds.Length == 0)
            return await ReleaseSelectionAsync(userId, request.BookingId, ct);

        // Kiểm tra ghế thuộc đúng suất chiếu và ghế vật lý còn dùng được.
        var seatRows = await db.ShowtimeSeats
            .AsNoTracking()
            .Where(ss => ss.ShowtimeId == showtime.ShowtimeId && desiredIds.Contains(ss.ShowtimeSeatId))
            .Select(ss => new { ss.ShowtimeSeatId, ss.SeatId, SeatStatus = ss.Seat.Status })
            .ToListAsync(ct);

        if (seatRows.Count != desiredIds.Length)
            throw new BookingException("Một hoặc nhiều ghế không thuộc suất chiếu này. Vui lòng tải lại sơ đồ ghế.", 404);

        if (seatRows.Any(s => s.SeatStatus != "Active"))
            throw new BookingException("Một hoặc nhiều ghế đang bảo trì, không thể chọn. Vui lòng chọn ghế khác.", 409);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Tìm hoặc tạo đơn giữ ghế.
            Booking booking;
            if (request.BookingId is int existingId)
            {
                booking = await LoadOwnedPendingBookingAsync(userId, existingId, now, ct)
                          ?? throw new BookingException("Đơn đặt vé không còn hiệu lực. Vui lòng chọn ghế lại.", 409);
            }
            else
            {
                booking = new Booking
                {
                    BookingCode = await GenerateBookingCodeAsync(ct),
                    CustomerId = await GetCustomerIdAsync(userId, ct),
                    ShowtimeId = showtime.ShowtimeId,
                    BookingDate = now,
                    // Hạn giữ cố định 5 phút kể từ lần chọn đầu tiên.
                    ExpiresAt = now.AddMinutes(BookingStates.HoldMinutes),
                    Status = BookingStates.BookingPending,
                    BookingChannel = "Online",
                    TotalAmount = 0m
                };
                db.Bookings.Add(booking);
                // Phải lưu đơn trước để có khóa chính làm giá trị HeldByBookingId của ghế.
                await db.SaveChangesAsync(ct);
            }

            // Ghế đang do chính đơn này giữ: giữ nguyên, không gia hạn hạn.
            var alreadyMine = await db.ShowtimeSeats
                .Where(ss => ss.ShowtimeId == showtime.ShowtimeId
                         && desiredIds.Contains(ss.ShowtimeSeatId)
                         && ss.HeldByBookingId == booking.BookingId
                         && ss.Status == BookingStates.SeatHeld)
                .Select(ss => ss.ShowtimeSeatId)
                .ToListAsync(ct);

            foreach (var seatId in desiredIds)
            {
                if (alreadyMine.Contains(seatId)) continue;

                // UPDATE nguyên tử: chỉ giữ được nếu ghế còn trống hoặc đã hết hạn.
                var acquired = await seats.TryAcquireAsync(showtime.ShowtimeId, seatId, booking.BookingId,
                    booking.ExpiresAt, now, ct);

                if (!acquired)
                    throw new BookingException(
                        "Một hoặc nhiều ghế vừa được người khác chọn. Lựa chọn của bạn chưa được giữ, vui lòng chọn lại.",
                        409);
            }

            // Nhả các ghế đang giữ mà khách không chọn nữa.
            await seats.ReleaseAsync(booking.BookingId, desiredIds, ct);

            // Tổng tiền luôn tính từ giá ghế trên máy chủ.
            var total = await db.ShowtimeSeats
                .Where(ss => ss.ShowtimeId == showtime.ShowtimeId
                         && desiredIds.Contains(ss.ShowtimeSeatId)
                         && ss.HeldByBookingId == booking.BookingId
                         && ss.Status == BookingStates.SeatHeld)
                .SumAsync(ss => ss.Price, ct);

            booking.TotalAmount = total;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await NotifySafelyAsync(showtime.ShowtimeId, ct);

            return new HoldResultViewModel(booking.BookingId, booking.ExpiresAt, clock.GetUtcNow().UtcDateTime, total);
        }
        catch (BookingException)
        {
            // Hoàn tác toàn bộ: không để lại ghế giữ dở hay đơn không có ghế.
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Bỏ chọn toàn bộ ghế: giải phóng ghế và chuyển đơn sang trạng thái đã hủy.</summary>
    private async Task<HoldResultViewModel> ReleaseSelectionAsync(int userId, int? bookingId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (bookingId is not int id) return new HoldResultViewModel(null, null, now, 0m);

        var booking = await LoadOwnedPendingBookingAsync(userId, id, now, ct);
        if (booking is null) return new HoldResultViewModel(null, null, now, 0m);

        await seats.ReleaseAllAsync(booking.BookingId, ct);
        booking.Status = BookingStates.BookingCancelled;
        booking.TotalAmount = 0m;
        await db.SaveChangesAsync(ct);

        await NotifySafelyAsync(booking.ShowtimeId, ct);
        return new HoldResultViewModel(null, null, clock.GetUtcNow().UtcDateTime, 0m);
    }

    // ==================================================================
    // 3. Thanh toán giả lập (FR-09)
    // ==================================================================

    /// <summary>
    /// Thực hiện thanh toán. Trả về trạng thái của đơn sau khi xử lý.
    /// Gọi lại khi đơn đã xác nhận sẽ trả về <c>Confirmed</c> mà không tạo thêm thanh toán hay vé.
    /// </summary>
    public async Task<string> PayAsync(int userId, SimulatedPaymentRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await EnsureActiveCustomerAsync(userId, ct);

        if (request.Outcome is not ("Success" or "Failed" or "Cancelled"))
            throw new BookingException("Kết quả thanh toán không hợp lệ.", 400);

        var booking = await db.Bookings
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.BookingId == request.BookingId && b.Customer != null
                                     && b.Customer.UserId == userId, ct);

        if (booking is null)
            throw new BookingException("Không tìm thấy đơn đặt vé của bạn.", 404);

        // Đơn đã xác nhận: trả về kết quả cũ, không phát vé lần hai (idempotent).
        if (booking.Status == BookingStates.BookingConfirmed)
            return BookingStates.BookingConfirmed;

        if (booking.Status != BookingStates.BookingPending)
            throw new BookingException("Đơn đặt vé không còn hiệu lực để thanh toán.", 409);

        // Hết hạn giữ ghế: đánh dấu hết hạn và trả ghế về trạng thái trống.
        if (booking.ExpiresAt <= now)
        {
            await ExpireBookingAsync(booking, ct);
            throw new BookingException("Đơn đặt vé đã hết thời gian giữ ghế. Vui lòng chọn ghế lại.", 409);
        }

        var outcome = request.Outcome;

        if (outcome == "Failed")
            return await HandleFailedPaymentAsync(booking, request, ct);

        if (outcome == "Cancelled")
            return await HandleCancelledPaymentAsync(booking, request, ct);

        return await HandleSuccessfulPaymentAsync(booking, request, now, ct);
    }

    /// <summary>
    /// Thanh toán thất bại: ghi nhận giao dịch lỗi, giữ nguyên ghế và hạn giữ để khách thử lại.
    /// Đơn không được chuyển sang trạng thái xác nhận.
    /// </summary>
    private async Task<string> HandleFailedPaymentAsync(Booking booking, SimulatedPaymentRequest request, CancellationToken ct)
    {
        var result = await gateway.ChargeAsync(booking.BookingId, booking.BookingCode, booking.TotalAmount,
            request.Method, request.Outcome, ct);

        db.Payments.Add(new Payment
        {
            BookingId = booking.BookingId,
            PaymentCode = await GeneratePaymentCodeAsync(ct),
            Provider = request.Method,
            TransactionRef = result.TransactionRef,
            Amount = booking.TotalAmount,
            PaymentMethod = request.Method,
            PaymentStatus = BookingStates.PaymentFailed,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            PaidAt = null // CHECK constraint: chỉ giao dịch thành công mới có PaidAt.
        });
        await db.SaveChangesAsync(ct);

        Logger.LogInformation("Thanh toán thất bại cho đơn {BookingCode}. Ghế vẫn được giữ đến {ExpiresAt:o}.",
            booking.BookingCode, booking.ExpiresAt);
        return BookingStates.BookingPending;
    }

    /// <summary>Khách hủy thanh toán: giải phóng ghế và hủy đơn.</summary>
    private async Task<string> HandleCancelledPaymentAsync(Booking booking, SimulatedPaymentRequest request, CancellationToken ct)
    {
        var result = await gateway.ChargeAsync(booking.BookingId, booking.BookingCode, booking.TotalAmount,
            request.Method, request.Outcome, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await seats.ReleaseAllAsync(booking.BookingId, ct);

            booking.Status = BookingStates.BookingCancelled;
            db.Payments.Add(new Payment
            {
                BookingId = booking.BookingId,
                PaymentCode = await GeneratePaymentCodeAsync(ct),
                Provider = request.Method,
                TransactionRef = result.TransactionRef,
                Amount = booking.TotalAmount,
                PaymentMethod = request.Method,
                PaymentStatus = BookingStates.PaymentCancelled,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                PaidAt = null
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (BookingException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await NotifySafelyAsync(booking.ShowtimeId, ct);
        return BookingStates.BookingCancelled;
    }

    /// <summary>
    /// Thanh toán thành công: chuyển đơn sang xác nhận, ghế sang đã bán và phát vé — tất cả trong
    /// một transaction để không xảy ra trường hợp có vé nhưng ghế lại chưa bán.
    /// </summary>
    private async Task<string> HandleSuccessfulPaymentAsync(Booking booking, SimulatedPaymentRequest request,
        DateTime now, CancellationToken ct)
    {
        var result = await gateway.ChargeAsync(booking.BookingId, booking.BookingCode, booking.TotalAmount,
            request.Method, request.Outcome, ct);

        // Ghế đang thực sự do đơn này giữ (đọc trước khi chuyển sang đã bán).
        var heldSeats = await db.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == booking.ShowtimeId
                     && ss.HeldByBookingId == booking.BookingId
                     && ss.Status == BookingStates.SeatHeld)
            .Select(ss => new { ss.ShowtimeSeatId, ss.Price })
            .ToListAsync(ct);

        if (heldSeats.Count == 0)
            throw new BookingException("Ghế của đơn đã được giải phóng. Vui lòng chọn ghế và thanh toán lại.", 409);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Cập nhật đơn trước. RowVersion làm nhiệm vụ chống thanh toán trùng:
            // nếu hai request thanh toán chạy song song, request thua sẽ bị từ chối cập nhật.
            booking.Status = BookingStates.BookingConfirmed;
            await db.SaveChangesAsync(ct);

            // Chuyển ghế sang đã bán; nếu thiếu ghế nghĩa là đã mất quyền giữ -> hủy giao dịch.
            var soldCount = await seats.MarkSoldAsync(booking.BookingId, booking.ShowtimeId, ct);
            if (soldCount != heldSeats.Count)
                throw new BookingException("Một hoặc nhiều ghế không còn được giữ. Thanh toán không thực hiện được.", 409);

            db.Payments.Add(new Payment
            {
                BookingId = booking.BookingId,
                PaymentCode = await GeneratePaymentCodeAsync(ct),
                Provider = request.Method,
                TransactionRef = result.TransactionRef,
                Amount = booking.TotalAmount,
                PaymentMethod = request.Method,
                PaymentStatus = BookingStates.PaymentSuccess,
                CreatedAt = now,
                PaidAt = now
            });

            // Phát mỗi ghế một vé, mã vé duy nhất và suy ra được từ mã đơn.
            var ticketCodes = new List<string>();
            foreach (var seat in heldSeats)
            {
                var code = $"TK-{booking.BookingCode}-{seat.ShowtimeSeatId}";
                ticketCodes.Add(code);
                db.Tickets.Add(new Ticket
                {
                    TicketCode = code,
                    BookingId = booking.BookingId,
                    ShowtimeSeatId = seat.ShowtimeSeatId,
                    ShowtimeId = booking.ShowtimeId,
                    Price = seat.Price,
                    Status = "Valid",
                    IssuedAt = now
                });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await NotifySafelyAsync(booking.ShowtimeId, ct);
            await emailSender.SendAsync(booking.BookingCode, ticketCodes, ct);

            Logger.LogInformation("Thanh toán thành công cho đơn {BookingCode}, phát {TicketCount} vé.",
                booking.BookingCode, ticketCodes.Count);
            return BookingStates.BookingConfirmed;
        }
        catch (BookingException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Request thua trong cuộc đua thanh toán: đơn có thể đã được xác nhận bởi request kia.
            await tx.RollbackAsync(ct);
            var current = await db.Bookings.AsNoTracking()
                .Where(b => b.BookingId == booking.BookingId)
                .Select(b => b.Status)
                .FirstOrDefaultAsync(ct);

            if (current == BookingStates.BookingConfirmed) return BookingStates.BookingConfirmed;
            throw new BookingException("Đơn đặt vé vừa được xử lý bởi thao tác khác. Vui lòng tải lại trang.", 409);
        }
    }

    // ==================================================================
    // 4. Hủy đơn
    // ==================================================================

    /// <summary>Hủy đơn đang giữ ghế và trả ghế về trạng thái trống.</summary>
    public async Task CancelAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await EnsureActiveCustomerAsync(userId, ct);

        var booking = await db.Bookings
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Customer != null
                                     && b.Customer.UserId == userId, ct);

        if (booking is null)
            throw new BookingException("Không tìm thấy đơn đặt vé của bạn.", 404);

        if (booking.Status == BookingStates.BookingConfirmed)
            throw new BookingException("Đơn đã thanh toán không thể hủy. Vui lòng liên hệ quầy vé.", 409);

        if (booking.Status != BookingStates.BookingPending)
            throw new BookingException("Đơn đặt vé không còn hiệu lực để hủy.", 409);

        await seats.ReleaseAllAsync(booking.BookingId, ct);
        booking.Status = BookingStates.BookingCancelled;
        await db.SaveChangesAsync(ct);

        await NotifySafelyAsync(booking.ShowtimeId, ct);
    }

    // ==================================================================
    // 5. Worker giải phóng ghế hết hạn
    // ==================================================================

    /// <summary>
    /// Quét và giải phóng các đơn đã hết hạn giữ ghế.
    /// Dùng UPDATE có điều kiện trạng thái nên nếu thanh toán xảy ra đồng thời và thắng,
    /// worker sẽ không ghi đè đơn đã xác nhận.
    /// </summary>
    public async Task<int> ReleaseExpiredAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var expiredIds = await db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStates.BookingPending && b.ExpiresAt <= now)
            .Select(b => b.BookingId)
            .ToListAsync(ct);

        var affected = 0;
        foreach (var id in expiredIds)
        {
            // Chỉ chuyển sang Expired nếu đơn vẫn còn ở trạng thái Pending.
            var changed = await db.Bookings
                .Where(b => b.BookingId == id && b.Status == BookingStates.BookingPending && b.ExpiresAt <= now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, BookingStates.BookingExpired), ct);

            if (changed == 0) continue; // Thanh toán đã thắng cuộc đua, đơn không còn hết hạn.

            // Chỉ giải phóng ghế vẫn còn thuộc đơn này (ghế đã bị người khác giành lại thì giữ nguyên).
            await seats.ReleaseAllAsync(id, ct);
            affected++;
        }

        if (affected > 0)
            Logger.LogInformation("Đã giải phóng {Count} đơn hết hạn giữ ghế.", affected);

        return affected;
    }

    // ==================================================================
    // 6. Vé điện tử
    // ==================================================================

    /// <summary>
    /// Sinh ảnh QR cho một vé. Kiểm tra chủ sở hữu: chỉ chính khách đặt vé mới tải được ảnh.
    /// </summary>
    public async Task<byte[]?> GetTicketQrAsync(int userId, int ticketId, CancellationToken ct = default)
    {
        var ticketCode = await db.Tickets
            .AsNoTracking()
            .Where(t => t.TicketId == ticketId && t.Booking.Customer != null && t.Booking.Customer.UserId == userId)
            .Select(t => t.TicketCode)
            .FirstOrDefaultAsync(ct);

        if (ticketCode is null) return null;

        // Nội dung mã QR chính là mã vé đã lưu, dùng để tra cứu tại quầy.
        return qr.GeneratePng(ticketCode);
    }

    // ==================================================================
    // Nội bộ dùng chung
    // ==================================================================

    /// <summary>
    /// Đảm bảo người dùng là khách hàng còn hoạt động.
    /// Lớp bảo vệ bắt buộc vì mọi thao tác ghi đều dựa trên userId lấy từ Session.
    /// </summary>
    private async Task EnsureActiveCustomerAsync(int userId, CancellationToken ct)
    {
        if (userId <= 0)
            throw new BookingException("Bạn cần đăng nhập để thực hiện thao tác này.", 403);

        var valid = await db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.User.Status == "Active", ct);

        if (!valid)
            throw new BookingException("Tài khoản không hợp lệ hoặc đã bị khóa.", 403);
    }

    private Task<int> GetCustomerIdAsync(int userId, CancellationToken ct) =>
        db.Customers.AsNoTracking().Where(c => c.UserId == userId).Select(c => c.CustomerId).FirstAsync(ct);

    /// <summary>Lấy đơn đang chờ thanh toán của chính khách; null nếu không thuộc về khách hoặc đã hết hạn.</summary>
    private Task<Booking?> LoadOwnedPendingBookingAsync(int userId, int bookingId, DateTime now, CancellationToken ct) =>
        db.Bookings
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId
                                  && b.Status == BookingStates.BookingPending
                                  && b.ExpiresAt > now
                                  && b.Customer != null && b.Customer.UserId == userId, ct);

    /// <summary>
    /// Đánh dấu đơn hết hạn và trả ghế về trạng thái trống.
    /// Dùng UPDATE có điều kiện trạng thái nên không phát sinh xung đột phiên bản khi
    /// worker đang chạy song song với yêu cầu thanh toán của khách.
    /// </summary>
    private async Task ExpireBookingAsync(Booking booking, CancellationToken ct)
    {
        await seats.ReleaseAllAsync(booking.BookingId, ct);

        // Chỉ đơn còn ở trạng thái Pending mới được chuyển sang Expired.
        await db.Bookings
            .Where(b => b.BookingId == booking.BookingId && b.Status == BookingStates.BookingPending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, BookingStates.BookingExpired), ct);

        await NotifySafelyAsync(booking.ShowtimeId, ct);
    }

    /// <summary>
    /// Phát tín hiệu SignalR cho các trình duyệt khác.
    /// Lỗi phát tín hiệu không được làm hỏng giao dịch đã commit nên được nuốt và ghi log.
    /// </summary>
    private async Task NotifySafelyAsync(int showtimeId, CancellationToken ct)
    {
        try
        {
            await notifier.NotifyAsync(showtimeId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "Không gửi được tín hiệu SignalR cho suất chiếu {ShowtimeId}.", showtimeId);
        }
    }

    /// <summary>Sinh mã đơn dạng BK-<ngày>-<mã ngẫu nhiên>, đảm bảo duy nhất.</summary>
    private async Task<string> GenerateBookingCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"BK-{clock.GetUtcNow():yyyyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
            var exists = await db.Bookings.AsNoTracking().AnyAsync(b => b.BookingCode == code, ct);
            if (!exists) return code;
        }

        throw new BookingException("Không tạo được mã đặt vé. Vui lòng thử lại.", 500);
    }

    /// <summary>Sinh mã giao dịch thanh toán duy nhất.</summary>
    private async Task<string> GeneratePaymentCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"PM-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";
            var exists = await db.Payments.AsNoTracking().AnyAsync(p => p.PaymentCode == code, ct);
            if (!exists) return code;
        }

        throw new BookingException("Không tạo được mã giao dịch thanh toán. Vui lòng thử lại.", 500);
    }
}
