using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Hubs;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Services;
using PRN222_CinemaManagement.Services.OnlineBooking;
using Xunit;

namespace OnlineBooking.Tests;

/// <summary>Mỗi thao tác mở DbContext mới như mỗi request HTTP thực tế.</summary>
public class BookingTests(BookingDatabase database) : IClassFixture<BookingDatabase>
{
    [Fact]
    public async Task Cac_service_duoc_DI_tao_theo_scope_hoan_thanh_luong_giu_ghe_va_thanh_toan()
    {
        var s = await database.SeedAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        // Tập trung vào scope nghiệp vụ, không khởi động hạ tầng HTTP/SignalR trong bài test.
        services.AddSingleton<IHubContext<SeatHub>>(new TestHubContext(false));
        services.AddScoped<CinemaDbContext>(_ => database.CreateContext());
        services.AddScoped<BookingSupportService>();
        services.AddScoped<MovieCatalogService>();
        services.AddScoped<SeatBookingService>();
        services.AddScoped<BookingPaymentService>();
        services.AddScoped<BookingTicketService>();
        services.AddScoped<IOnlineBookingService, OnlineBookingService>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        int bookingId;
        // Hai scope mô phỏng hai request độc lập; kết quả phải được lưu qua database.
        await using (var holdScope = provider.CreateAsyncScope())
        {
            var service = holdScope.ServiceProvider.GetRequiredService<IOnlineBookingService>();
            var held = await service.SetSeatsAsync(s.UserId, new SetSeatsRequest
            {
                ShowtimeId = s.ShowtimeId, ShowtimeSeatIds = [s.Seats[0], s.Seats[3]]
            });
            bookingId = held.BookingId!.Value;
            Assert.Equal(220_000m, held.TotalAmount);
        }

        await using (var paymentScope = provider.CreateAsyncScope())
        {
            var service = paymentScope.ServiceProvider.GetRequiredService<IOnlineBookingService>();
            Assert.Equal("Confirmed", await service.PayAsync(s.UserId, new SimulatedPaymentRequest
            {
                BookingId = bookingId, Outcome = "Success", Method = "VNPay_Simulated"
            }));
            var booking = await service.GetBookingAsync(s.UserId, bookingId);
            Assert.NotNull(booking);
            Assert.Equal("Confirmed", booking.Status);
            Assert.Equal(2, booking.Tickets.Count);
        }
    }

    private async Task<HoldResultViewModel> Hold(Scenario s, int userId, int[] seatIds, int? bookingId = null, int? showtimeId = null)
    {
        await using var db = database.CreateContext();
        return await BookingDatabase.Service(db).SetSeatsAsync(userId, new SetSeatsRequest
        {
            ShowtimeId = showtimeId ?? s.ShowtimeId, BookingId = bookingId, ShowtimeSeatIds = seatIds
        });
    }

    private async Task<string> Pay(int userId, int bookingId, string outcome = "Success")
    {
        await using var db = database.CreateContext();
        return await BookingDatabase.Service(db).PayAsync(userId,
            new SimulatedPaymentRequest { BookingId = bookingId, Outcome = outcome, Method = "VNPay_Simulated" });
    }

    [Fact]
    public async Task VnPay_callback_luu_dung_phuong_thuc_va_co_du_lieu_hien_thi_ve()
    {
        var scenario = await database.SeedAsync();
        var hold = await Hold(scenario, scenario.UserId, [scenario.Seats[0]]);
        var bookingId = hold.BookingId!.Value;

        // Gọi đúng hàm mà VnPayReturn sử dụng, trên SQL Server có CHECK constraint thật.
        await using (var db = database.CreateContext())
        {
            var status = await BookingDatabase.Service(db)
                .ConfirmPaymentAsync(bookingId, true, CancellationToken.None);
            Assert.Equal("Confirmed", status);
        }

        // F5 callback không được tạo thêm thanh toán hoặc vé.
        await using (var db = database.CreateContext())
        {
            var status = await BookingDatabase.Service(db)
                .ConfirmPaymentAsync(bookingId, true, CancellationToken.None);
            Assert.Equal("Confirmed", status);
        }

        await using var check = database.CreateContext();
        var payment = Assert.Single(await check.Payments.Where(p => p.BookingId == bookingId).ToListAsync());
        Assert.Equal("VNPay", payment.Provider);
        Assert.Equal("VNPay_Simulated", payment.PaymentMethod);
        Assert.Equal("Success", payment.PaymentStatus);
        Assert.Equal(hold.TotalAmount, payment.Amount);

        // Trang Success đọc được đơn đã xác nhận, thông tin ghế và ảnh QR của vé.
        var service = BookingDatabase.Service(check);
        var details = await service.GetBookingAsync(scenario.UserId, bookingId);
        Assert.NotNull(details);
        Assert.Equal("Confirmed", details!.Status);
        Assert.Single(details.Seats);
        var ticket = Assert.Single(details.Tickets);
        Assert.NotNull(await service.GetTicketQrAsync(scenario.UserId, ticket.TicketId));
        Assert.Equal("Sold", (await check.ShowtimeSeats.FindAsync(scenario.Seats[0]))!.Status);
    }

    [Fact]
    public async Task Hai_khach_tranh_cung_ghe_chi_mot_nguoi_giu_thanh_cong()
    {
        var s = await database.SeedAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Claim(int userId)
        {
            await start.Task;
            try { await Hold(s, userId, [s.Seats[0]]); return true; }
            catch (BookingException) { return false; }
        }
        var attempts = new[] { Claim(s.UserId), Claim(s.OtherUserId) };
        start.SetResult();
        Assert.Single(await Task.WhenAll(attempts), result => result);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Bookings.CountAsync(b => b.ShowtimeId == s.ShowtimeId));
        Assert.Equal("Held", (await db.ShowtimeSeats.FindAsync(s.Seats[0]))!.Status);
    }

    [Fact]
    public async Task Xung_dot_mot_ghe_rollback_toan_bo_lua_chon_va_khong_de_don_rac()
    {
        var s = await database.SeedAsync();
        var first = await Hold(s, s.OtherUserId, [s.Seats[1]]);
        await Assert.ThrowsAsync<BookingException>(() => Hold(s, s.UserId, [s.Seats[0], s.Seats[1]]));
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Bookings.CountAsync(b => b.ShowtimeId == s.ShowtimeId));
        Assert.Equal("Available", (await db.ShowtimeSeats.FindAsync(s.Seats[0]))!.Status);
        Assert.Equal(first.BookingId, (await db.ShowtimeSeats.FindAsync(s.Seats[1]))!.HeldByBookingId);
    }

    [Fact]
    public async Task Doi_ghe_tinh_gia_tu_server_va_khong_gia_han_giu_ghe()
    {
        var s = await database.SeedAsync();
        var first = await Hold(s, s.UserId, [s.Seats[0]]);
        var second = await Hold(s, s.UserId, [s.Seats[1], s.Seats[3]], first.BookingId);
        Assert.Equal(first.BookingId, second.BookingId);
        Assert.Equal(first.ExpiresAt, second.ExpiresAt);
        Assert.Equal(220_000m, second.TotalAmount);
        await using var db = database.CreateContext();
        var released = await db.ShowtimeSeats.FindAsync(s.Seats[0]);
        Assert.Equal("Available", released!.Status);
        Assert.Null(released.HeldByBookingId);
        Assert.Null(released.HoldExpiresAt);
    }

    [Fact]
    public async Task Worker_het_han_don_va_nha_ghe_nhung_khong_dung_vao_ghe_da_ban()
    {
        var s = await database.SeedAsync();
        var pending = await Hold(s, s.UserId, [s.Seats[0]]);
        var sold = await Hold(s, s.OtherUserId, [s.Seats[1]]);
        await Pay(s.OtherUserId, sold.BookingId!.Value);
        await database.ExpireAsync(pending.BookingId!.Value);
        await using var db = database.CreateContext();
        await BookingDatabase.Service(db).ReleaseExpiredAsync();
        await using var check = database.CreateContext();
        Assert.Equal("Expired", (await check.Bookings.FindAsync(pending.BookingId))!.Status);
        var released = await check.ShowtimeSeats.FindAsync(s.Seats[0]);
        Assert.Equal("Available", released!.Status);
        Assert.Null(released.HeldByBookingId);
        Assert.Null(released.HoldExpiresAt);
        Assert.Equal("Sold", (await check.ShowtimeSeats.FindAsync(s.Seats[1]))!.Status);
    }

    [Fact]
    public async Task Thanh_toan_lap_khong_tao_them_ve_va_qr_chi_chu_don_duoc_xem()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0], s.Seats[3]]);
        Assert.Equal("Confirmed", await Pay(s.UserId, hold.BookingId!.Value));
        Assert.Equal("Confirmed", await Pay(s.UserId, hold.BookingId.Value));
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Payments.CountAsync(p => p.BookingId == hold.BookingId && p.PaymentStatus == "Success"));
        var tickets = await db.Tickets.Where(t => t.BookingId == hold.BookingId).ToListAsync();
        Assert.Equal(2, tickets.Count);
        Assert.Equal(220_000m, tickets.Sum(t => t.Price));
        Assert.Equal(2, tickets.Select(t => t.TicketCode).Distinct().Count());
        var sold = await db.ShowtimeSeats.Where(x => x.ShowtimeId == s.ShowtimeId && x.Status == "Sold").ToListAsync();
        Assert.Equal(2, sold.Count);
        Assert.All(sold, x => { Assert.Null(x.HeldByBookingId); Assert.Null(x.HoldExpiresAt); });
        var service = BookingDatabase.Service(db);
        var qr = await service.GetTicketQrAsync(s.UserId, tickets[0].TicketId);
        Assert.NotNull(qr);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, qr!.Take(8).ToArray());
        Assert.Null(await service.GetTicketQrAsync(s.OtherUserId, tickets[0].TicketId));
    }

    [Fact]
    public async Task Hai_thanh_toan_dong_thoi_chi_ghi_mot_payment_va_mot_ve()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0]]);
        var results = await Task.WhenAll(Pay(s.UserId, hold.BookingId!.Value), Pay(s.UserId, hold.BookingId.Value));
        Assert.All(results, result => Assert.Equal("Confirmed", result));
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Tickets.CountAsync(t => t.BookingId == hold.BookingId));
        Assert.Equal(1, await db.Payments.CountAsync(p => p.BookingId == hold.BookingId && p.PaymentStatus == "Success"));
        Assert.Equal("Confirmed", await Pay(s.UserId, hold.BookingId.Value, "Failed"));
    }

    [Fact]
    public async Task Thanh_toan_that_bai_giu_nguyen_han_va_co_the_thu_lai()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0]]);
        Assert.Equal("Pending", await Pay(s.UserId, hold.BookingId!.Value, "Failed"));
        await using (var db = database.CreateContext())
        {
            var seat = await db.ShowtimeSeats.FindAsync(s.Seats[0]);
            Assert.Equal("Held", seat!.Status);
            Assert.Equal(hold.ExpiresAt, seat.HoldExpiresAt);
            Assert.Equal(0, await db.Tickets.CountAsync(t => t.BookingId == hold.BookingId));
            Assert.Equal(1, await db.Payments.CountAsync(p => p.BookingId == hold.BookingId && p.PaymentStatus == "Failed"));
        }
        Assert.Equal("Confirmed", await Pay(s.UserId, hold.BookingId.Value));
    }

    [Fact]
    public async Task Huy_thanh_toan_nha_toan_bo_ghe_va_khong_the_thanh_toan_sau_do()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0], s.Seats[1]]);
        Assert.Equal("Cancelled", await Pay(s.UserId, hold.BookingId!.Value, "Cancelled"));
        await using var db = database.CreateContext();
        Assert.All(await db.ShowtimeSeats.Where(x => x.ShowtimeId == s.ShowtimeId).ToListAsync(), x =>
        {
            Assert.Equal("Available", x.Status); Assert.Null(x.HeldByBookingId); Assert.Null(x.HoldExpiresAt);
        });
        await Assert.ThrowsAsync<BookingException>(() => Pay(s.UserId, hold.BookingId.Value));
    }

    [Fact]
    public async Task Khach_khac_khong_the_xem_doi_ghe_huy_hoac_thanh_toan_don()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0]]);
        await Assert.ThrowsAsync<BookingException>(() => Hold(s, s.OtherUserId, [s.Seats[1]], hold.BookingId));
        await Assert.ThrowsAsync<BookingException>(() => Pay(s.OtherUserId, hold.BookingId!.Value));
        await using var db = database.CreateContext();
        var service = BookingDatabase.Service(db);
        Assert.Null(await service.GetBookingAsync(s.OtherUserId, hold.BookingId!.Value));
        await Assert.ThrowsAsync<BookingException>(() => service.CancelAsync(s.OtherUserId, hold.BookingId.Value));
        Assert.Equal("Held", (await db.ShowtimeSeats.FindAsync(s.Seats[0]))!.Status);
    }

    [Fact]
    public async Task Tai_khoan_bi_khoa_khong_the_giu_ghe_hoac_thanh_toan()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0]]);
        await using (var db = database.CreateContext())
            await db.Users.Where(u => u.UserId == s.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, "Locked"));
        await Assert.ThrowsAsync<BookingException>(() => Hold(s, s.UserId, [s.Seats[1]]));
        await Assert.ThrowsAsync<BookingException>(() => Pay(s.UserId, hold.BookingId!.Value));
    }

    [Fact]
    public async Task Tu_choi_ghe_sai_suat_ghe_khoa_va_ghe_hong()
    {
        var s = await database.SeedAsync();
        await Assert.ThrowsAsync<BookingException>(() => Hold(s, s.UserId, [s.OtherSeats[0]]));
        await using (var db = database.CreateContext())
        {
            await db.ShowtimeSeats.Where(x => x.ShowtimeSeatId == s.Seats[0]).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, "Blocked"));
            var physicalId = await db.ShowtimeSeats.Where(x => x.ShowtimeSeatId == s.Seats[1]).Select(x => x.SeatId).SingleAsync();
            await db.Seats.Where(x => x.SeatId == physicalId).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, "Broken"));
        }
        await Assert.ThrowsAsync<BookingException>(() => Hold(s, s.UserId, [s.Seats[0]]));
        await Assert.ThrowsAsync<BookingException>(() => Hold(s, s.UserId, [s.Seats[1]]));
        await Hold(s, s.OtherUserId, [s.OtherSeats[0]], showtimeId: s.OtherShowtimeId);
    }

    [Fact]
    public async Task Chu_cu_khong_the_ban_hoac_nha_ghe_da_duoc_khach_khac_giu_lai()
    {
        var s = await database.SeedAsync();
        var old = await Hold(s, s.UserId, [s.Seats[0]]);
        await database.ExpireAsync(old.BookingId!.Value);
        var current = await Hold(s, s.OtherUserId, [s.Seats[0]]);
        await Assert.ThrowsAsync<BookingException>(() => Pay(s.UserId, old.BookingId.Value));
        await using var db = database.CreateContext();
        await BookingDatabase.Service(db).ReleaseExpiredAsync();
        var seat = await db.ShowtimeSeats.FindAsync(s.Seats[0]);
        Assert.Equal("Held", seat!.Status);
        Assert.Equal(current.BookingId, seat.HeldByBookingId);
        Assert.Equal("Confirmed", await Pay(s.OtherUserId, current.BookingId!.Value));
    }

    [Fact]
    public async Task Worker_va_thanh_toan_dong_thoi_sau_het_han_khong_phat_ve()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0]]);
        await database.ExpireAsync(hold.BookingId!.Value);
        await using var workerDb = database.CreateContext();
        await Task.WhenAll(BookingDatabase.Service(workerDb).ReleaseExpiredAsync(),
            Assert.ThrowsAsync<BookingException>(() => Pay(s.UserId, hold.BookingId.Value)));
        await using var db = database.CreateContext();
        Assert.Equal(0, await db.Tickets.CountAsync(t => t.BookingId == hold.BookingId));
        Assert.Equal("Available", (await db.ShowtimeSeats.FindAsync(s.Seats[0]))!.Status);
        Assert.Equal("Expired", (await db.Bookings.FindAsync(hold.BookingId))!.Status);
    }

    [Fact]
    public async Task Loi_luu_ve_rollback_ca_don_thanh_toan_va_trang_thai_ghe()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0], s.Seats[1]]);
        await using (var db = database.CreateContext(new FailTicketSave()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => BookingDatabase.Service(db).PayAsync(s.UserId,
                new SimulatedPaymentRequest { BookingId = hold.BookingId!.Value, Outcome = "Success", Method = "VNPay_Simulated" }));
        }
        await using var check = database.CreateContext();
        Assert.Equal("Pending", (await check.Bookings.FindAsync(hold.BookingId))!.Status);
        Assert.Equal(2, await check.ShowtimeSeats.CountAsync(x => x.HeldByBookingId == hold.BookingId && x.Status == "Held"));
        Assert.Equal(0, await check.Tickets.CountAsync(x => x.BookingId == hold.BookingId));
        Assert.Equal(0, await check.Payments.CountAsync(x => x.BookingId == hold.BookingId));
    }

    [Fact]
    public async Task Loi_signalr_sau_commit_khong_dao_nguoc_don_da_thanh_toan()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0]]);
        await using var db = database.CreateContext();
        Assert.Equal("Confirmed", await BookingDatabase.Service(db, failNotification: true).PayAsync(s.UserId,
            new SimulatedPaymentRequest { BookingId = hold.BookingId!.Value, Outcome = "Success", Method = "VNPay_Simulated" }));
        await using var check = database.CreateContext();
        Assert.Equal("Confirmed", (await check.Bookings.FindAsync(hold.BookingId))!.Status);
        Assert.Equal(1, await check.Tickets.CountAsync(t => t.BookingId == hold.BookingId));
    }

    [Fact]
    public async Task Doc_danh_muc_khong_tao_don_va_an_phim_suat_chieu_da_dong()
    {
        var s = await database.SeedAsync();
        await using var db = database.CreateContext();
        var service = BookingDatabase.Service(db);
        Assert.NotNull(await service.GetMovieAsync(s.MovieId));
        Assert.NotNull(await service.GetSeatMapAsync(s.ShowtimeId, null));
        Assert.Equal(0, await db.Bookings.CountAsync(b => b.ShowtimeId == s.ShowtimeId));
        await db.Showtimes.Where(x => x.ShowtimeId == s.ShowtimeId).ExecuteUpdateAsync(x => x
            .SetProperty(y => y.StartAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Null(await service.GetSeatMapAsync(s.ShowtimeId, null));
        await db.Movies.Where(x => x.MovieId == s.MovieId).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, "Ended"));
        Assert.Null(await service.GetMovieAsync(s.MovieId));
        Assert.DoesNotContain(await service.GetMoviesAsync(), movie => movie.MovieId == s.MovieId);
    }

    [Fact]
    public async Task Bo_het_ghe_huy_don_cu_va_khong_tao_don_moi()
    {
        var s = await database.SeedAsync();
        var hold = await Hold(s, s.UserId, [s.Seats[0]]);
        var result = await Hold(s, s.UserId, [], hold.BookingId);
        Assert.Null(result.BookingId);
        await using var db = database.CreateContext();
        Assert.Equal("Cancelled", (await db.Bookings.FindAsync(hold.BookingId))!.Status);
        Assert.Equal("Available", (await db.ShowtimeSeats.FindAsync(s.Seats[0]))!.Status);
        Assert.Equal(1, await db.Bookings.CountAsync(b => b.ShowtimeId == s.ShowtimeId));
    }
}
