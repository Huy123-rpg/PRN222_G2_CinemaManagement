using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Constants;
using PRN222_CinemaManagement.Exceptions;
using PRN222_CinemaManagement.Models.ViewModels;
using PRN222_CinemaManagement.Services.Interfaces;

namespace PRN222_CinemaManagement.Controllers;

// Khách chưa đăng nhập vẫn xem được phim và sơ đồ ghế.
// Các action GET chỉ đọc dữ liệu, không tạo đơn hoặc giữ ghế.
public class MoviesController(IOnlineBookingService bookingService) : Controller
{
    // GET: /Movies - Danh sách phim đang mở bán (hỗ trợ tìm kiếm, lọc và sắp xếp).
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MovieFilterViewModel filter, CancellationToken ct)
    {
        var catalog = await bookingService.GetMovieCatalogAsync(filter, ct);
        return View(catalog);
    }

    // GET: /Movies/Details/5 - Chi tiết phim và các suất chiếu sắp tới.
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var movie = await bookingService.GetMovieAsync(id, ct);
        if (movie == null)
        {
            return NotFound("Phim không tồn tại hoặc chưa mở bán.");
        }
        return View(movie);
    }

    // GET: /Movies/Seats/5 - Sơ đồ ghế của một suất chiếu.
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Seats(int id, CancellationToken ct)
    {
        try
        {
            var userId = HttpContext.Session.GetInt32(BookingConstants.UserIdSessionKey);
            var map = await bookingService.GetSeatMapAsync(id, userId, ct);
            if (map == null)
            {
                return NotFound("Suất chiếu không tồn tại hoặc đã ngừng nhận đặt vé.");
            }
            return View(map);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    // GET: /Movies/SeatSnapshot/5 - Đọc lại sơ đồ mới nhất khi có thông báo SignalR.
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SeatSnapshot(int id, CancellationToken ct)
    {
        try
        {
            var userId = HttpContext.Session.GetInt32(BookingConstants.UserIdSessionKey);
            var map = await bookingService.GetSeatMapAsync(id, userId, ct);
            if (map == null)
            {
                return NotFound(new { message = "Suất chiếu đã đóng. Vui lòng chọn suất khác." });
            }
            return Json(map);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
