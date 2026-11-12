using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public sealed class ScreeningScheduleException(string message) : InvalidOperationException(message)
{
    public List<ScreeningConflictViewModel> Conflicts { get; } = [];
}
