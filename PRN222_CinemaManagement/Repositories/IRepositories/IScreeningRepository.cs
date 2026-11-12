using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface IScreeningRepository
{
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<Movie?> GetMovieAsync(int movieId, CancellationToken cancellationToken);
    Task<Screening?> GetScreeningAsync(int screeningId, CancellationToken cancellationToken);
    Task<bool> AuditoriumIsActiveAsync(int auditoriumId, CancellationToken cancellationToken);
    Task<bool> HasBookingOrTicketAsync(int screeningId, CancellationToken cancellationToken);
    Task<bool> HasScheduleConflictAsync(int auditoriumId, DateTime startAt, DateTime endAt, int? exceptScreeningId, CancellationToken cancellationToken);
    void Add(Screening screening);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
