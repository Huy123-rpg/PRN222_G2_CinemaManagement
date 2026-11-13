using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Repositories.IRepositories;

public interface IScreeningRepository
{
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<Movie?> GetMovieAsync(int movieId, CancellationToken cancellationToken);
    Task<Screening?> GetScreeningAsync(int screeningId, CancellationToken cancellationToken);
    Task<List<Movie>> GetSchedulableMoviesAsync(CancellationToken cancellationToken);
    Task<List<Auditorium>> GetActiveAuditoriumsAsync(CancellationToken cancellationToken);
    Task<List<Seat>> GetActiveSeatsAsync(int auditoriumId, CancellationToken cancellationToken);
    Task<List<Screening>> GetScreeningsAsync(DateTime fromUtc, DateTime toUtc, int? auditoriumId, CancellationToken cancellationToken);
    Task<bool> AuditoriumIsActiveAsync(int auditoriumId, CancellationToken cancellationToken);
    Task<bool> HasBookingOrTicketAsync(int screeningId, CancellationToken cancellationToken);
    Task<List<Screening>> GetScheduleConflictsAsync(int auditoriumId, DateTime startAt, DateTime endAt, int? exceptScreeningId, CancellationToken cancellationToken);
    Task<List<Screening>> GetScreeningsOverlappingAsync(IReadOnlyCollection<int> auditoriumIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    void Add(Screening screening);
    void AddScreeningSeats(IEnumerable<ScreeningSeat> seats);
    void RemoveScreeningSeats(IEnumerable<ScreeningSeat> seats);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
