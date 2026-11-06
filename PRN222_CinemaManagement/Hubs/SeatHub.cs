using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;

namespace PRN222_CinemaManagement.Hubs;

/// <summary>Kênh chỉ phát tín hiệu làm mới sơ đồ, không phát dữ liệu cá nhân hay nhận lệnh đặt vé.</summary>
public class SeatHub(CinemaDbContext db) : Hub
{
    public static string GroupName(int showtimeId) => $"showtime:{showtimeId}";

    public async Task JoinShowtime(int showtimeId)
    {
        if (!await db.Showtimes.AnyAsync(s => s.ShowtimeId == showtimeId && s.Movie.Status == "Showing",
                Context.ConnectionAborted))
            throw new HubException("Không tìm thấy suất chiếu.");

        // Mỗi kết nối theo dõi một suất; reconnect cần gọi lại để đăng ký nhóm mới.
        if (Context.Items.TryGetValue("ShowtimeGroup", out var previous) && previous is string oldGroup)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroup);
        var group = GroupName(showtimeId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        Context.Items["ShowtimeGroup"] = group;
    }
}
