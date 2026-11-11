using PRN222_CinemaManagement.BackgroundServices;
using PRN222_CinemaManagement.Repositories.Implementations;
using PRN222_CinemaManagement.Repositories.Interfaces;
using PRN222_CinemaManagement.Services.Implementations;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IMovieRepository, MovieRepository>();
        services.AddScoped<IShowtimeRepository, ShowtimeRepository>();
        services.AddScoped<IShowtimeSeatRepository, ShowtimeSeatRepository>();
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
        services.AddRepositories();
        services.AddOnlineBookingServices();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddHostedService<SeatHoldExpirationService>();

        return services;
    }
}
