using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.IService;

/// <summary>
/// Session user snapshot returned after successful authentication.
/// </summary>
public record AuthenticatedUserInfo(int UserId, string Username, string FullName, string RoleName);

/// <summary>
/// Business service contract for account authentication, profile management, and password updates.
/// </summary>
public interface IAccountService
{
    Task<ServiceResult<AuthenticatedUserInfo>> AuthenticateAsync(LoginViewModel model);
    Task<ProfileViewModel?> GetProfileAsync(int userId);
    Task<UpdateProfileViewModel?> GetUpdateProfileAsync(int userId);
    Task<ServiceResult<string>> UpdateProfileAsync(int userId, UpdateProfileViewModel model);
    Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordViewModel model);
}
