using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PRN222_CinemaManagement.Filters;

/// <summary>
/// Custom Filter kiểm tra quyền đăng nhập dựa trên Session (chuẩn môn PRN222).
/// Cách dùng:
///   [SessionAuthorize] -> Bắt buộc phải đăng nhập
///   [SessionAuthorize(Roles = "Admin")] -> Bắt buộc là Admin
///   [SessionAuthorize(Roles = "Admin,CinemaManager")] -> Admin hoặc Quản lý
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : ActionFilterAttribute
{
    public string? Roles { get; set; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        var username = session.GetString("Username");
        var role = session.GetString("Role");

        // 1. Kiểm tra đăng nhập
        if (string.IsNullOrEmpty(username))
        {
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        // 2. Kiểm tra vai trò nếu có yêu cầu
        if (!string.IsNullOrEmpty(Roles))
        {
            var allowedRoles = Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }
        }

        base.OnActionExecuting(context);
    }
}
