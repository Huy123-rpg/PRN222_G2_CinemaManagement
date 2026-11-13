using Microsoft.AspNetCore.SignalR;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Hubs;

/// <summary>Kênh chỉ phát tín hiệu làm mới sơ đồ, không phát dữ liệu cá nhân hay nhận lệnh đặt vé.</summary>
public class SeatHub(IMovieCatalogService movieCatalogService) : Hub
{
    public static string GroupName(int screeningId)
    {
        return "screening:" + screeningId;
    }

    public async Task JoinScreening(int screeningId)
    {
        bool exists = await movieCatalogService.IsScreeningAvailableForUpdatesAsync(screeningId, Context.ConnectionAborted);
        if (!exists)
        {
            throw new HubException("Không tìm thấy suất chiếu.");
        }

        // Mỗi kết nối theo dõi một suất; reconnect cần gọi lại để đăng ký nhóm mới.
        if (Context.Items.ContainsKey("ScreeningGroup"))
        {
            var oldGroup = Context.Items["ScreeningGroup"] as string;
            if (oldGroup != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroup);
            }
        }

        var group = GroupName(screeningId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        Context.Items["ScreeningGroup"] = group;
    }
}
