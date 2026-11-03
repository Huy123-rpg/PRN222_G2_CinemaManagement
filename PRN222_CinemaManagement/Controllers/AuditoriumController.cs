using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

[SessionAuthorize(Roles = "CinemaManager,Admin")]
public class AuditoriumController(CinemaDbContext context) : Controller
{
    // GET: /Auditorium (UC-25 - Danh sách phòng chiếu)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var auditoriums = await context.Auditoriums
            .Include(a => a.Seats)
                .ThenInclude(s => s.SeatType)
            .Include(a => a.Showtimes)
            .OrderBy(a => a.AuditoriumId)
            .Select(a => new AuditoriumListItemViewModel
            {
                AuditoriumId = a.AuditoriumId,
                Name = a.Name,
                TotalRows = a.TotalRows,
                TotalColumns = a.TotalColumns,
                TotalSeats = a.Seats.Count,
                ActiveSeatsCount = a.Seats.Count(s => s.Status == "Active"),
                VipSeatsCount = a.Seats.Count(s => s.SeatType.TypeName == "VIP" && s.Status == "Active"),
                CoupleSeatsCount = a.Seats.Count(s => s.SeatType.TypeName == "Couple" && s.Status == "Active"),
                Status = a.Status,
                HasShowtimes = a.Showtimes.Any()
            })
            .ToListAsync();

        return View(auditoriums);
    }

    // GET: /Auditorium/Create (UC-25 - Thêm phòng chiếu)
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.SeatTypes = await context.SeatTypes.Where(st => st.IsActive).ToListAsync();
        return View(new AuditoriumCreateViewModel());
    }

    // POST: /Auditorium/Create (UC-25 - Lưu phòng chiếu & Tự động tạo ma trận ghế)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AuditoriumCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.SeatTypes = await context.SeatTypes.Where(st => st.IsActive).ToListAsync();
            return View(model);
        }

        string trimmedName = model.Name.Trim();
        bool nameExists = await context.Auditoriums.AnyAsync(a => a.Name.ToLower() == trimmedName.ToLower());
        if (nameExists)
        {
            ModelState.AddModelError("Name", "Tên phòng chiếu này đã tồn tại trong hệ thống.");
            ViewBag.SeatTypes = await context.SeatTypes.Where(st => st.IsActive).ToListAsync();
            return View(model);
        }

        var defaultSeatType = await context.SeatTypes.FindAsync(model.DefaultSeatTypeId) 
            ?? await context.SeatTypes.FirstOrDefaultAsync(st => st.TypeName == "Standard");

        int seatTypeId = defaultSeatType?.SeatTypeId ?? 1;

        // 1. Tạo bản ghi phòng chiếu
        var auditorium = new Auditorium
        {
            Name = trimmedName,
            TotalRows = model.TotalRows,
            TotalColumns = model.TotalColumns,
            Status = model.Status
        };

        context.Auditoriums.Add(auditorium);
        await context.SaveChangesAsync();

        // 2. Khởi tạo ma trận sơ đồ ghế tự động (UC-25)
        var seats = new List<Seat>();
        for (int r = 1; r <= model.TotalRows; r++)
        {
            string rowLabel = ((char)('A' + r - 1)).ToString();
            for (int c = 1; c <= model.TotalColumns; c++)
            {
                seats.Add(new Seat
                {
                    AuditoriumId = auditorium.AuditoriumId,
                    RowLabel = rowLabel,
                    SeatNumber = c,
                    SeatTypeId = seatTypeId,
                    Status = "Active"
                });
            }
        }

        context.Seats.AddRange(seats);
        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo thành công '{auditorium.Name}' với {seats.Count} ghế. Bạn có thể cấu hình chi tiết loại ghế ngay dưới đây.";
        return RedirectToAction(nameof(ConfigureSeats), new { id = auditorium.AuditoriumId });
    }

    // GET: /Auditorium/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var auditorium = await context.Auditoriums
            .Include(a => a.Showtimes)
            .FirstOrDefaultAsync(a => a.AuditoriumId == id);

        if (auditorium == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phòng chiếu yêu cầu.";
            return RedirectToAction(nameof(Index));
        }

        var model = new AuditoriumEditViewModel
        {
            AuditoriumId = auditorium.AuditoriumId,
            Name = auditorium.Name,
            TotalRows = auditorium.TotalRows,
            TotalColumns = auditorium.TotalColumns,
            Status = auditorium.Status,
            HasShowtimes = auditorium.Showtimes.Any()
        };

        return View(model);
    }

    // POST: /Auditorium/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AuditoriumEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var auditorium = await context.Auditoriums.FindAsync(model.AuditoriumId);
        if (auditorium == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phòng chiếu.";
            return RedirectToAction(nameof(Index));
        }

        string trimmedName = model.Name.Trim();
        bool nameExists = await context.Auditoriums.AnyAsync(a => a.AuditoriumId != model.AuditoriumId && a.Name.ToLower() == trimmedName.ToLower());
        if (nameExists)
        {
            ModelState.AddModelError("Name", "Tên phòng chiếu đã được sử dụng.");
            return View(model);
        }

        auditorium.Name = trimmedName;
        auditorium.Status = model.Status;

        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Cập nhật thông tin phòng chiếu '{auditorium.Name}' thành công.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Auditorium/ConfigureSeats/{id} (UC-25 & UC-26 - Sơ đồ ghế & Cấu hình loại ghế)
    [HttpGet]
    public async Task<IActionResult> ConfigureSeats(int id)
    {
        var auditorium = await context.Auditoriums
            .Include(a => a.Seats)
                .ThenInclude(s => s.SeatType)
            .FirstOrDefaultAsync(a => a.AuditoriumId == id);

        if (auditorium == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phòng chiếu.";
            return RedirectToAction(nameof(Index));
        }

        var seatTypes = await context.SeatTypes.OrderBy(st => st.SeatTypeId).ToListAsync();

        var viewModel = new AuditoriumSeatConfigViewModel
        {
            AuditoriumId = auditorium.AuditoriumId,
            AuditoriumName = auditorium.Name,
            TotalRows = auditorium.TotalRows,
            TotalColumns = auditorium.TotalColumns,
            Status = auditorium.Status,
            AvailableSeatTypes = seatTypes,
            Seats = auditorium.Seats
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .Select(s => new SeatItemViewModel
                {
                    SeatId = s.SeatId,
                    RowLabel = s.RowLabel,
                    SeatNumber = s.SeatNumber,
                    SeatTypeId = s.SeatTypeId,
                    SeatTypeName = s.SeatType.TypeName,
                    PriceMultiplier = s.SeatType.PriceMultiplier,
                    Status = s.Status
                })
                .ToList()
        };

        return View(viewModel);
    }

    // POST: /Auditorium/UpdateSeats (UC-26 - Cập nhật hàng loạt loại ghế hoặc trạng thái ghế)
    [HttpPost]
    public async Task<IActionResult> UpdateSeats([FromBody] UpdateSeatsRequestModel request)
    {
        if (request.SeatIds == null || request.SeatIds.Count == 0)
        {
            return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 ghế để cập nhật." });
        }

        var seatsToUpdate = await context.Seats
            .Where(s => s.AuditoriumId == request.AuditoriumId && request.SeatIds.Contains(s.SeatId))
            .ToListAsync();

        if (seatsToUpdate.Count == 0)
        {
            return Json(new { success = false, message = "Không tìm thấy ghế tương ứng." });
        }

        if (request.SeatTypeId.HasValue)
        {
            var seatType = await context.SeatTypes.FindAsync(request.SeatTypeId.Value);
            if (seatType != null)
            {
                foreach (var seat in seatsToUpdate)
                {
                    seat.SeatTypeId = request.SeatTypeId.Value;
                }
            }
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            var allowedStatuses = new[] { "Active", "Broken", "Inactive" };
            if (allowedStatuses.Contains(request.Status))
            {
                foreach (var seat in seatsToUpdate)
                {
                    seat.Status = request.Status;
                }
            }
        }

        await context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"Đã cập nhật thành công {seatsToUpdate.Count} ghế."
        });
    }

    // POST: /Auditorium/ToggleStatus/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var auditorium = await context.Auditoriums.FindAsync(id);
        if (auditorium == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phòng chiếu.";
            return RedirectToAction(nameof(Index));
        }

        auditorium.Status = auditorium.Status == "Active" ? "Maintenance" : "Active";
        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã đổi trạng thái phòng '{auditorium.Name}' sang '{auditorium.Status}'.";
        return RedirectToAction(nameof(Index));
    }
}
