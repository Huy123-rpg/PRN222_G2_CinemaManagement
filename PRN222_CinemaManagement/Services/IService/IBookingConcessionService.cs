using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

public interface IBookingConcessionService
{
    Task<ConcessionSelectionViewModel?> GetSelectionPageAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<ConcessionSelectionResultViewModel> SaveSelectionAsync(int userId, SetConcessionsRequest request, CancellationToken ct = default);
}

