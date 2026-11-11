using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.Interfaces;

public interface IBookingConcessionRepository : IGenericRepository<BookingConcession>
{
    Task<IReadOnlyList<BookingConcession>> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<decimal> GetSubtotalByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<int> DeleteByBookingIdAsync(int bookingId, CancellationToken ct = default);
}

