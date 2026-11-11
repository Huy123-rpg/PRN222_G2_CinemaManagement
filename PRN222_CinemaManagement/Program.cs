using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Services;
using PRN222_CinemaManagement.Services.OnlineBooking;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CinemaDb")));

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddHttpContextAccessor();

// Các nghiệp vụ đặt vé dùng Scoped để cùng chia sẻ DbContext trong một request.
builder.Services.AddScoped<BookingSupportService>();
builder.Services.AddScoped<MovieCatalogService>();
builder.Services.AddScoped<SeatBookingService>();
builder.Services.AddScoped<BookingPaymentService>();
builder.Services.AddScoped<BookingTicketService>();
// Controller và worker vẫn gọi interface cũ qua lớp chuyển tiếp nhỏ.
builder.Services.AddScoped<IOnlineBookingService, OnlineBookingService>();

// ===== SignalR: kênh cập nhật sơ đồ ghế thời gian thực =====
builder.Services.AddSignalR();
// ===== VNPay: thanh toán online =====
builder.Services.AddScoped<VnPayService>();

// ===== Worker nền: quét và nhả ghế của các đơn đã hết hạn giữ =====
builder.Services.AddHostedService<SeatHoldExpirationService>();

// Cấu hình Session chuẩn môn PRN222 (không dùng Cookie/JWT phức tạp)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Seed database khi khởi động
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<CinemaDbContext>();
        var passwordService = services.GetRequiredService<IPasswordService>();
        await DbInitializer.SeedAsync(context, passwordService);

        // Dữ liệu demo cho module đặt vé: phim, suất chiếu, lưới ghế và khách hàng phụ.
        // Chỉ bổ sung khi thiếu, không ghi đè dữ liệu thật.
        await BookingDemoData.SeedAsync(context);
        await BookingDemoData.SeedDemoCustomersAsync(context, passwordService);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Kích hoạt Session middleware
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR hub cập nhật sơ đồ ghế: /hubs/seats (đường dẫn client JS trong Seats.cshtml dùng).
app.MapHub<PRN222_CinemaManagement.Hubs.SeatHub>("/hubs/seats");

app.Run();
