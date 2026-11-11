using Microsoft.AspNetCore.SignalR;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Hubs;

/// <summary>Kênh chỉ phát tín hiệu làm mới sơ đồ, không phát dữ liệu cá nhân hay nhận lệnh đặt vé.</summary>
public class SeatHub(IMovieCatalogService movieCatalogService) : Hub
{
    public static string GroupName(int showtimeId)
    {
        return "showtime:" + showtimeId;
    }

    public async Task JoinShowtime(int showtimeId)
    {
        bool exists = await movieCatalogService.IsShowtimeAvailableForUpdatesAsync(showtimeId, Context.ConnectionAborted);
        if (!exists)
        {
            throw new HubException("Không tìm thấy suất chiếu.");
        }

        // Mỗi kết nối theo dõi một suất; reconnect cần gọi lại để đăng ký nhóm mới.
        if (Context.Items.ContainsKey("ShowtimeGroup"))
        {
            var oldGroup = Context.Items["ShowtimeGroup"] as string;
            if (oldGroup != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroup);
            }
        }

        var group = GroupName(showtimeId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        Context.Items["ShowtimeGroup"] = group;
    }
}
