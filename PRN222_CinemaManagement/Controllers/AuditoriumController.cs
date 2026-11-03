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
    // 1. GET: /Auditorium - Danh sách phòng chiếu (UC-25)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var auditoriums = await context.Auditoriums
            .Include(a => a.Seats)
                .ThenInclude(s => s.SeatType)
            .OrderBy(a => a.AuditoriumId)
            .ToListAsync();

        return View(auditoriums);
    }

    // 2. GET: /Auditorium/Create - Màn hình tạo phòng chiếu (UC-25)
    [HttpGet]
    public IActionResult Create()
    {
        return View(new AuditoriumCreateViewModel());
    }

    // 3. POST: /Auditorium/Create - Lưu phòng và tự động sinh ma trận ghế (UC-25)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AuditoriumCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Kiểm tra tên phòng đã tồn tại chưa
        string roomName = model.Name.Trim();
        bool isExist = await context.Auditoriums.AnyAsync(a => a.Name.ToLower() == roomName.ToLower());
        if (isExist)
        {
            ModelState.AddModelError("Name", "Tên phòng chiếu này đã tồn tại.");
            return View(model);
        }

        // Bước 1: Tạo phòng chiếu mới
        var auditorium = new Auditorium
        {
            Name = roomName,
            TotalRows = model.TotalRows,
            TotalColumns = model.TotalColumns,
            Status = model.Status
        };
        context.Auditoriums.Add(auditorium);
        await context.SaveChangesAsync();

        // Bước 2: Tìm loại ghế mặc định (Standard - ghế thường)
        var standardType = await context.SeatTypes.FirstOrDefaultAsync(st => st.TypeName == "Standard");
        int defaultSeatTypeId = standardType?.SeatTypeId ?? 1;

        // Bước 3: Tạo tự động toàn bộ ghế dựa vào số hàng và số cột
        var newSeats = new List<Seat>();
        for (int r = 1; r <= model.TotalRows; r++)
        {
            string rowLabel = ((char)('A' + r - 1)).ToString(); // A, B, C...
            for (int c = 1; c <= model.TotalColumns; c++)
            {
                newSeats.Add(new Seat
                {
                    AuditoriumId = auditorium.AuditoriumId,
                    RowLabel = rowLabel,
                    SeatNumber = c,
                    SeatTypeId = defaultSeatTypeId,
                    Status = "Active"
                });
            }
        }

        context.Seats.AddRange(newSeats);
        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo phòng '{auditorium.Name}' với {newSeats.Count} ghế thành công!";
        return RedirectToAction(nameof(ConfigureSeats), new { id = auditorium.AuditoriumId });
    }

    // 4. GET: /Auditorium/Edit/{id} - Màn hình sửa thông tin phòng (UC-25)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var auditorium = await context.Auditoriums.FindAsync(id);
        if (auditorium == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phòng chiếu.";
            return RedirectToAction(nameof(Index));
        }

        var model = new AuditoriumEditViewModel
        {
            AuditoriumId = auditorium.AuditoriumId,
            Name = auditorium.Name,
            Status = auditorium.Status,
            TotalRows = auditorium.TotalRows,
            TotalColumns = auditorium.TotalColumns
        };

        return View(model);
    }

    // 5. POST: /Auditorium/Edit/{id} - Cập nhật thông tin phòng (UC-25)
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

        auditorium.Name = model.Name.Trim();
        auditorium.Status = model.Status;
        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cập nhật phòng chiếu thành công!";
        return RedirectToAction(nameof(Index));
    }

    // 6. GET: /Auditorium/ConfigureSeats/{id} - Xem sơ đồ ghế (UC-25, UC-26)
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

        var seatTypes = await context.SeatTypes.Where(st => st.IsActive).ToListAsync();

        var viewModel = new ConfigureSeatsViewModel
        {
            Auditorium = auditorium,
            SeatTypes = seatTypes
        };

        return View(viewModel);
    }

    // 7. POST: /Auditorium/ConfigureSeats - Cập nhật loại ghế hoặc trạng thái bằng Form POST chuẩn MVC (UC-26)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfigureSeats(int auditoriumId, List<int> selectedSeatIds, int? seatTypeId, string? status)
    {
        // Kiểm tra người dùng đã tick chọn ghế nào chưa
        if (selectedSeatIds == null || selectedSeatIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng tick chọn ít nhất một ghế trên sơ đồ trước khi áp dụng.";
            return RedirectToAction(nameof(ConfigureSeats), new { id = auditoriumId });
        }

        // Lấy danh sách các ghế được chọn
        var seatsToUpdate = await context.Seats
            .Where(s => s.AuditoriumId == auditoriumId && selectedSeatIds.Contains(s.SeatId))
            .ToListAsync();

        // Đổi loại ghế (nếu có chọn)
        if (seatTypeId.HasValue && seatTypeId.Value > 0)
        {
            foreach (var seat in seatsToUpdate)
            {
                seat.SeatTypeId = seatTypeId.Value;
            }
        }

        // Đổi trạng thái (nếu có chọn)
        if (!string.IsNullOrEmpty(status))
        {
            foreach (var seat in seatsToUpdate)
            {
                seat.Status = status;
            }
        }

        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật thành công {seatsToUpdate.Count} ghế!";
        return RedirectToAction(nameof(ConfigureSeats), new { id = auditoriumId });
    }

    // 8. POST: /Auditorium/ToggleStatus/{id} - Bật/tắt nhanh bảo trì phòng
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var auditorium = await context.Auditoriums.FindAsync(id);
        if (auditorium != null)
        {
            auditorium.Status = auditorium.Status == "Active" ? "Maintenance" : "Active";
            await context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Phòng '{auditorium.Name}' chuyển sang trạng thái: {auditorium.Status}.";
        }
        return RedirectToAction(nameof(Index));
    }
}
