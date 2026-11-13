using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public sealed record SaveScreeningRequest(int MovieId, int AuditoriumId, DateTime StartAtLocal, decimal BasePrice);

public interface IScreeningScheduleService
{
    Task<ScreeningScheduleViewModel> GetScheduleAsync(DateOnly date, int? auditoriumId, int dayCount);
    Task<ScreeningFormViewModel> GetCreateFormAsync();
    Task<ScreeningBatchViewModel> GetBatchFormAsync(int? movieId, CancellationToken cancellationToken = default);
    Task<List<ScreeningBatchConflictViewModel>> CheckBatchConflictsAsync(ScreeningBatchViewModel model, CancellationToken cancellationToken = default);
    Task CreateBatchAsync(ScreeningBatchViewModel model, CancellationToken cancellationToken = default);
    Task<ScreeningFormViewModel?> GetEditFormAsync(int screeningId);
    Task<List<ScreeningConflictViewModel>> CheckConflictsAsync(ScreeningFormViewModel model);
    Task<Screening> CreateAsync(SaveScreeningRequest request, CancellationToken cancellationToken = default);
    Task<Screening> UpdateAsync(int screeningId, SaveScreeningRequest request, CancellationToken cancellationToken = default);
}
