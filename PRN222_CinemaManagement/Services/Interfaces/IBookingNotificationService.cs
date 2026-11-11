namespace PRN222_CinemaManagement.Services.Interfaces;

public interface IBookingNotificationService
{
    Task NotifySeatsChangedAsync(int showtimeId);
}

