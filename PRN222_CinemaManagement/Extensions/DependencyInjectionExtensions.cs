using PRN222_CinemaManagement.BackgroundServices;
using PRN222_CinemaManagement.Repositories.ImpleRepositories;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.ImpleService;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Extensions;
//class này dùng để đăng ký các Repository và Service của module đặt
//  vé online vào DI container, giúp tách biệt các phần của ứng dụng và
//  dễ dàng quản lý phụ thuộc.
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IMovieCatalogRepository, MovieCatalogRepository>();
        services.AddScoped<IScreeningBookingRepository, ScreeningBookingRepository>();
        services.AddScoped<IScreeningSeatRepository, ScreeningSeatRepository>();
        services.AddScoped<IConcessionRepository, ConcessionRepository>();
        services.AddScoped<IBookingConcessionRepository, BookingConcessionRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }

    public static IServiceCollection AddOnlineBookingServices(this IServiceCollection services)
    {
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddScoped<IBookingEmailService, BookingEmailService>();
        services.AddScoped<IBookingNotificationService, BookingNotificationService>();
        services.AddScoped<IBookingSupportService, BookingSupportService>();
        services.AddScoped<IMovieCatalogService, MovieCatalogService>();
        services.AddScoped<ISeatBookingService, SeatBookingService>();
        services.AddScoped<IBookingConcessionService, BookingConcessionService>();
        services.AddScoped<IBookingPaymentService, BookingPaymentService>();
        services.AddScoped<IBookingTicketService, BookingTicketService>();
        services.AddScoped<IOnlineBookingService, OnlineBookingService>();

        return services;
    }

    public static IServiceCollection AddOnlineBookingModule(this IServiceCollection services)
    {
        services.AddRepositories(); // Đăng ký các Repository
        services.AddOnlineBookingServices();// Đăng ký các Service
        services.AddScoped<IVnPayService, VnPayService>();// Đăng ký Service xử lý thanh toán VnPay
        services.AddHostedService<SeatHoldExpirationService>();// Đăng ký Worker nền xử lý hết hạn giữ ghế

        return services;
    }
}
