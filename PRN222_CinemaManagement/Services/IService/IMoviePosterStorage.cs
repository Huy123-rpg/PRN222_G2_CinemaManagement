using Microsoft.AspNetCore.Http;

namespace PRN222_CinemaManagement.Services.IService;

public interface IMoviePosterStorage
{
    Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? posterUrl);
}
