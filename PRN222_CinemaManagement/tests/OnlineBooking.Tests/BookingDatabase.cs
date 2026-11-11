using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Hubs;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Services;
using PRN222_CinemaManagement.Services.OnlineBooking;
using Xunit;

namespace OnlineBooking.Tests;

/// <summary>Database SQL Server riêng cho mỗi lần chạy; tuyệt đối không dùng CinemaDB của ứng dụng.</summary>
public class BookingDatabase : IAsyncLifetime
{
    private readonly string databaseName = "CinemaBookingTests_" + Guid.NewGuid().ToString("N");
    private string connectionString = "";

    public async Task InitializeAsync()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CINEMA_TEST_SQL")
            ?? "Server=.;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10");
        // Ghi đè tên database dù chuỗi kết nối được cấu hình từ biến môi trường.
        builder.InitialCatalog = databaseName;
        builder.MultipleActiveResultSets = false;
        connectionString = builder.ConnectionString;
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName
            || !databaseName.StartsWith("CinemaBookingTests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Không được xóa database ngoài phạm vi kiểm thử.");
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    public CinemaDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>().UseSqlServer(connectionString);
        if (interceptors.Length > 0) options.AddInterceptors(interceptors);
        return new CinemaDbContext(options.Options);
    }

    public static OnlineBookingService Service(CinemaDbContext db, bool failNotification = false)
    {
        // Cùng một DbContext như các service Scoped của ứng dụng; không mở kết nối riêng giữa các bước.
        var support = new BookingSupportService(db, new TestHubContext(failNotification),
            NullLogger<BookingSupportService>.Instance);
        return new OnlineBookingService(
            new MovieCatalogService(db, support),
            new SeatBookingService(db, support),
            new BookingPaymentService(db, support, NullLogger<BookingPaymentService>.Instance),
            new BookingTicketService(db, support));
    }

    public async Task<Scenario> SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        var users = Enumerable.Range(1, 2).Select(i => new User
        {
            Username = $"test{i}_{suffix}", Email = $"test{i}_{suffix}@example.test",
            FullName = $"Khách kiểm thử {i}", PasswordHash = "test-only-no-login", RoleId = 4, Status = "Active"
        }).ToArray();
        var room = new Auditorium { Name = "Test " + suffix, TotalRows = 1, TotalColumns = 4 };
        var movie = new Movie { Title = "Phim " + suffix, Status = "Showing", DurationMinutes = 90 };
        db.AddRange(users);
        db.AddRange(room, movie);
        await db.SaveChangesAsync();
        var customers = users.Select((u, i) => new Customer
        {
            UserId = u.UserId, CustomerCode = $"T{i}{suffix[..16]}"
        }).ToArray();
        var seats = Enumerable.Range(1, 4).Select(i => new Seat
        {
            AuditoriumId = room.AuditoriumId, SeatTypeId = i == 4 ? 2 : 1,
            RowLabel = "A", SeatNumber = i, Status = "Active"
        }).ToArray();
        var start = DateTime.UtcNow.AddDays(2);
        var shows = Enumerable.Range(1, 2).Select(i => new Showtime
        {
            MovieId = movie.MovieId, AuditoriumId = room.AuditoriumId, StartAt = start.AddHours(i * 3),
            EndAt = start.AddHours(i * 3 + 2), BasePrice = 100_000m, Status = "Scheduled"
        }).ToArray();
        db.AddRange(customers);
        db.AddRange(seats);
        db.AddRange(shows);
        await db.SaveChangesAsync();
        var showSeats = shows.SelectMany(show => seats.Select(seat => new ShowtimeSeat
        {
            ShowtimeId = show.ShowtimeId, AuditoriumId = room.AuditoriumId, SeatId = seat.SeatId,
            Price = seat.SeatNumber == 4 ? 120_000m : 100_000m, Status = "Available"
        })).ToArray();
        db.AddRange(showSeats);
        await db.SaveChangesAsync();
        return new Scenario(users[0].UserId, users[1].UserId, movie.MovieId, shows[0].ShowtimeId,
            shows[1].ShowtimeId, showSeats.Take(4).Select(s => s.ShowtimeSeatId).ToArray(),
            showSeats.Skip(4).Select(s => s.ShowtimeSeatId).ToArray());
    }

    /// <summary>Đưa đơn và ghế tới quá hạn nhưng vẫn thỏa CHECK ExpiresAt > BookingDate.</summary>
    public async Task ExpireAsync(int bookingId)
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        await db.Bookings.Where(b => b.BookingId == bookingId).ExecuteUpdateAsync(x => x
            .SetProperty(b => b.BookingDate, now.AddMinutes(-10)).SetProperty(b => b.ExpiresAt, now.AddMinutes(-1)));
        await db.ShowtimeSeats.Where(s => s.HeldByBookingId == bookingId)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.HoldExpiresAt, now.AddMinutes(-1)));
    }
}

public record Scenario(int UserId, int OtherUserId, int MovieId, int ShowtimeId, int OtherShowtimeId,
    int[] Seats, int[] OtherSeats);

// Thay kết nối SignalR bằng đối tượng đơn giản để test không cần mở trình duyệt hoặc socket.
public class TestHubContext(bool fail) : IHubContext<SeatHub>
{
    public IHubClients Clients { get; } = new TestHubClients(fail);
    public IGroupManager Groups { get; } = new TestGroups();
}

public class TestHubClients(bool fail) : IHubClients, IClientProxy
{
    public IClientProxy All => this;
    public IClientProxy AllExcept(IReadOnlyList<string> ids) => this;
    public IClientProxy Client(string id) => this;
    public IClientProxy Clients(IReadOnlyList<string> ids) => this;
    public IClientProxy Group(string name) => this;
    public IClientProxy GroupExcept(string name, IReadOnlyList<string> ids) => this;
    public IClientProxy Groups(IReadOnlyList<string> names) => this;
    public IClientProxy User(string id) => this;
    public IClientProxy Users(IReadOnlyList<string> ids) => this;
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        if (fail) throw new InvalidOperationException("Mô phỏng lỗi SignalR sau commit.");
        return Task.CompletedTask;
    }
}

public class TestGroups : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Gây lỗi khi lưu vé để kiểm tra rollback cả đơn, ghế và thanh toán.</summary>
public class FailTicketSave : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Ticket>().Any(e => e.State == EntityState.Added))
            throw new InvalidOperationException("Lỗi ghi vé chủ động trong kiểm thử.");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
