using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN222_CinemaManagement.Data;

namespace PRN222_CinemaManagement.Filters;

/// <summary>
/// Custom Filter kiểm tra quyền đăng nhập dựa trên Session (chuẩn môn PRN222).
/// Tích hợp cơ chế thu hồi quyền tức thì khi tài khoản bị khóa hoặc thay đổi vai trò.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : ActionFilterAttribute
{
    public string? Roles { get; set; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32("UserId");
        var username = session.GetString("Username");

        // 1. Kiểm tra đăng nhập
        if (userId == null || string.IsNullOrEmpty(username))
        {
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        // 2. Kiểm tra trạng thái tài khoản trực tiếp trong DbContext (thu hồi quyền tức thì khi bị khóa)
        var dbContext = context.HttpContext.RequestServices.GetService<CinemaDbContext>();
        if (dbContext != null)
        {
            var user = dbContext.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == userId.Value);
            if (user == null || user.Status != "Active")
            {
                session.Clear();
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // Tự động đồng bộ lại vai trò hiện tại vào Session nếu Admin vừa đổi role
            if (session.GetString("Role") != user.Role.RoleName)
            {
                session.SetString("Role", user.Role.RoleName);
            }
        }

        var role = session.GetString("Role");

        // 3. Kiểm tra vai trò nếu có yêu cầu
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
