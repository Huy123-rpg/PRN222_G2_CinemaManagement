namespace PRN222_CinemaManagement.Services.IService;

public interface IBookingNotificationService
{
    Task NotifySeatsChangedAsync(int screeningId);
}

