using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>
/// Implementation of <see cref="IAccountService"/>.
/// </summary>
public class AccountService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordService passwordService) : IAccountService
{
    public async Task<ServiceResult<AuthenticatedUserInfo>> AuthenticateAsync(LoginViewModel model)
    {
        var user = await userRepository.GetByUsernameWithRoleAsync(model.Username);

        if (user == null || !passwordService.VerifyPassword(model.Password, user.PasswordHash))
        {
            return ServiceResult<AuthenticatedUserInfo>.Fail(
                string.Empty,
                "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (user.Status != "Active")
        {
            return ServiceResult<AuthenticatedUserInfo>.Fail(
                string.Empty,
                "Tài khoản của bạn đã bị khóa hoặc không hoạt động. Vui lòng liên hệ Admin.");
        }

        return ServiceResult<AuthenticatedUserInfo>.Success(
            new AuthenticatedUserInfo(user.UserId, user.Username, user.FullName, user.Role.RoleName));
    }

    public async Task<ServiceResult> ValidateRegistrationAsync(RegisterViewModel model)
    {
        var errors = new List<ValidationFailure>();

        if (await userRepository.ExistsByUsernameAsync(model.Username.Trim()))
        {
            errors.Add(new ValidationFailure("Username", "Tên đăng nhập này đã tồn tại."));
        }

        if (await userRepository.ExistsByEmailAsync(model.Email.Trim()))
        {
            errors.Add(new ValidationFailure("Email", "Email này đã được sử dụng."));
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RegisterAsync(RegisterViewModel model)
    {
        var validation = await ValidateRegistrationAsync(model);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var customerRole = await roleRepository.GetByNameAsync("Customer");
        if (customerRole == null)
        {
            return ServiceResult.Fail(string.Empty, "Không tìm thấy cấu hình vai trò Khách hàng.");
        }

        try
        {
            await userRepository.ExecuteInTransactionAsync(async () =>
            {
                var user = new User
                {
                    Username = model.Username.Trim(),
                    FullName = model.FullName.Trim(),
                    Email = model.Email.Trim(),
                    PhoneNumber = model.PhoneNumber?.Trim(),
                    RoleId = customerRole.RoleId,
                    Status = "Active",
                    PasswordHash = passwordService.HashPassword(model.Password),
                    CreatedAt = DateTime.UtcNow
                };

                await userRepository.AddUserAsync(user);

                var customer = new Customer
                {
                    UserId = user.UserId,
                    CustomerCode = $"CUS{user.UserId:D4}"
                };

                userRepository.AddCustomer(customer);
                await userRepository.SaveChangesAsync();
            });

            return ServiceResult.Success("Đăng ký tài khoản thành công! Vui lòng đăng nhập.");
        }
        catch (Exception ex)
        {
            return ServiceResult.Fail(string.Empty, "Có lỗi xảy ra khi đăng ký tài khoản: " + ex.Message);
        }
    }

    public async Task<ProfileViewModel?> GetProfileAsync(int userId)
    {
        var user = await userRepository.GetByIdWithDetailsAsync(userId);
        if (user == null)
        {
            return null;
        }

        string? code = user.Admin?.AdminCode
            ?? user.CinemaManager?.ManagerCode
            ?? user.Staff?.StaffCode
            ?? user.Customer?.CustomerCode;

        return new ProfileViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleName = user.Role.RoleName,
            UserCode = code,
            Status = user.Status,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UpdateProfileViewModel?> GetUpdateProfileAsync(int userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        return new UpdateProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        };
    }

    public async Task<ServiceResult<string>> UpdateProfileAsync(int userId, UpdateProfileViewModel model)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return ServiceResult<string>.NotFound();
        }

        if (await userRepository.ExistsByEmailAsync(model.Email, userId))
        {
            return ServiceResult<string>.Fail("Email", "Email này đã được sử dụng bởi tài khoản khác.");
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.PhoneNumber = model.PhoneNumber;

        await userRepository.SaveChangesAsync();

        return ServiceResult<string>.Success(user.FullName);
    }

    public async Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordViewModel model)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return ServiceResult.NotFound();
        }

        if (!passwordService.VerifyPassword(model.CurrentPassword, user.PasswordHash))
        {
            return ServiceResult.Fail("CurrentPassword", "Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = passwordService.HashPassword(model.NewPassword);
        await userRepository.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
