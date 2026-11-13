using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface ITicketRepository : IGenericRepository<Ticket>
{
    Task<Ticket?> GetConfirmedOnlineTicketForQrAsync(int customerId, int ticketId, CancellationToken ct = default);
}

