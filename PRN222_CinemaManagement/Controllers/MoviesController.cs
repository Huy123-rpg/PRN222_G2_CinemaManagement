using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Services;

namespace PRN222_CinemaManagement.Controllers;

/// <summary>Danh mục công khai; mọi thao tác đọc không tạo đơn hay tự giữ ghế.</summary>
public class MoviesController(IOnlineBookingService bookingService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await bookingService.GetMoviesAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var movie = await bookingService.GetMovieAsync(id, ct);
        return movie is null ? NotFound("Phim không tồn tại hoặc chưa mở bán.") : View(movie);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Seats(int id, CancellationToken ct)
    {
        var map = await bookingService.GetSeatMapAsync(id, HttpContext.Session.GetInt32("UserId"), ct);
        return map is null ? NotFound("Suất chiếu không tồn tại hoặc đã ngừng nhận đặt vé.") : View(map);
    }

    /// <summary>SignalR chỉ báo có thay đổi; client đọc lại snapshot để không áp dụng sự kiện cũ sai thứ tự.</summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SeatSnapshot(int id, CancellationToken ct)
    {
        var map = await bookingService.GetSeatMapAsync(id, HttpContext.Session.GetInt32("UserId"), ct);
        return map is null
            ? NotFound(new { message = "Suất chiếu đã đóng. Vui lòng chọn suất khác." })
            : Json(map);
    }
}
