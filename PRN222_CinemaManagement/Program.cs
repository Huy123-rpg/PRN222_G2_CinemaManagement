using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Repositories.ImpleRepositories;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Extensions;
using PRN222_CinemaManagement.Services;
using PRN222_CinemaManagement.Services.ImpleService;
using PRN222_CinemaManagement.Services.IService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CinemaDb")));

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IScreeningRepository, ScreeningRepository>();
builder.Services.AddScoped<IScreeningScheduleService, ScreeningScheduleService>();
builder.Services.AddScoped<IMovieRepository, MovieRepository>();
builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddSingleton<IMoviePosterStorage, MoviePosterStorage>();
builder.Services.AddHttpContextAccessor();

// ===== Module đặt vé online: Repository, Service và Worker nền =====
builder.Services.AddOnlineBookingModule();

// ===== SignalR: kênh cập nhật sơ đồ ghế thời gian thực =====
builder.Services.AddSignalR();

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

        if (app.Environment.IsDevelopment())
        {
            // Dữ liệu mẫu chỉ được tạo ở môi trường phát triển.
            await BookingDemoData.SeedAsync(context);
            await BookingDemoData.SeedDemoCustomersAsync(context, passwordService);
        }
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
