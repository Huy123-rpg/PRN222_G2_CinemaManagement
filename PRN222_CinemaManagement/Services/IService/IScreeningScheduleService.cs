using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Services.IService;

public sealed record SaveScreeningRequest(int MovieId, int AuditoriumId, DateTime StartAt, decimal BasePrice);

public interface IScreeningScheduleService
{
    Task<Screening> CreateAsync(SaveScreeningRequest request, CancellationToken cancellationToken = default);
    Task<Screening> UpdateAsync(int screeningId, SaveScreeningRequest request, CancellationToken cancellationToken = default);
}
