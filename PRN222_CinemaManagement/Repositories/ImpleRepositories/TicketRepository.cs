using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class TicketRepository(CinemaDbContext context)
    : GenericRepository<Ticket>(context), ITicketRepository
{
    public Task<Ticket?> GetConfirmedOnlineTicketForQrAsync(int customerId, int ticketId, CancellationToken ct = default)
    {
        return DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId
                && t.Booking.CustomerId == customerId
                && t.Booking.BookingChannel == BookingConstants.Channels.Online
                && t.Booking.Status == BookingConstants.BookingStatuses.Confirmed
                && t.Status != BookingConstants.TicketStatuses.Cancelled, ct);
    }
}

