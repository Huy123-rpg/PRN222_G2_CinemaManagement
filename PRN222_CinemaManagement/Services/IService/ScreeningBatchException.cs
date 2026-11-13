using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public sealed class ScreeningBatchException(string message, List<ScreeningBatchConflictViewModel> conflicts) : InvalidOperationException(message)
{
    public List<ScreeningBatchConflictViewModel> Conflicts { get; } = conflicts;
}
