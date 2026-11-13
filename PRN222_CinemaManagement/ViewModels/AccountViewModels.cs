using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? UserCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateProfileViewModel
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    [StringLength(254)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class EmployeeItemViewModel
{
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class EmployeeListViewModel
{
    public string? SearchKeyword { get; set; }
    public string? RoleFilter { get; set; }
    public string? StatusFilter { get; set; }
    public List<EmployeeItemViewModel> Employees { get; set; } = new();
}

public class CreateEmployeeViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    [StringLength(254)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò")]
    [Display(Name = "Vai trò")]
    public int RoleId { get; set; } // 2: CinemaManager, 3: Staff

    [Display(Name = "Mã nhân viên (tùy chọn)")]
    public string? EmployeeCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu khởi tạo")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "123456";
}

public class EditEmployeeViewModel
{
    public int UserId { get; set; }

    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Mã nhân viên")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    [StringLength(254)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò")]
    [Display(Name = "Vai trò")]
    public int RoleId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Active"; // Active, Inactive, Locked
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    [StringLength(254)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyEmailViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã xác thực")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác thực gồm 6 chữ số")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Mã xác thực chỉ chứa 6 chữ số")]
    [Display(Name = "Mã xác thực (OTP)")]
    public string OtpCode { get; set; } = string.Empty;

    public string? Email { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email tài khoản của bạn")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    [Display(Name = "Email tài khoản")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã xác thực OTP")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác thực gồm 6 chữ số")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Mã xác thực chỉ chứa 6 chữ số")]
    [Display(Name = "Mã xác thực (OTP)")]
    public string OtpCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}



