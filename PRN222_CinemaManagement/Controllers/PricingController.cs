using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

[SessionAuthorize(Roles = "CinemaManager,Admin")]
public class PricingController(CinemaDbContext context) : Controller
{
    // 1. GET: /Pricing - Xem danh sách loại ghế & bảng tính giá mô phỏng (UC-27)
    [HttpGet]
    public async Task<IActionResult> Index(decimal? sampleBasePrice)
    {
        var seatTypes = await context.SeatTypes
            .Include(st => st.Seats)
            .OrderBy(st => st.SeatTypeId)
            .ToListAsync();

        ViewBag.SampleBasePrice = sampleBasePrice ?? 80000m;
        return View(seatTypes);
    }

    // 2. GET: /Pricing/EditSeatType/{id} - Màn hình sửa hệ số giá (UC-27)
    [HttpGet]
    public async Task<IActionResult> EditSeatType(int id)
    {
        var seatType = await context.SeatTypes.FindAsync(id);
        if (seatType == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy loại ghế.";
            return RedirectToAction(nameof(Index));
        }

        var model = new EditSeatTypeViewModel
        {
            SeatTypeId = seatType.SeatTypeId,
            TypeName = seatType.TypeName,
            Description = seatType.Description,
            PriceMultiplier = seatType.PriceMultiplier,
            IsActive = seatType.IsActive
        };

        return View(model);
    }

    // 3. POST: /Pricing/EditSeatType - Lưu hệ số giá mới (UC-27)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSeatType(EditSeatTypeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var seatType = await context.SeatTypes.FindAsync(model.SeatTypeId);
        if (seatType == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy loại ghế.";
            return RedirectToAction(nameof(Index));
        }

        // Kiểm tra tên loại ghế có bị trùng với loại ghế khác không
        string typeName = model.TypeName.Trim();
        bool isExist = await context.SeatTypes.AnyAsync(st => st.SeatTypeId != model.SeatTypeId && st.TypeName.ToLower() == typeName.ToLower());
        if (isExist)
        {
            ModelState.AddModelError("TypeName", "Tên loại ghế này đã tồn tại.");
            return View(model);
        }

        seatType.TypeName = typeName;
        seatType.Description = model.Description?.Trim();
        seatType.PriceMultiplier = model.PriceMultiplier;
        seatType.IsActive = model.IsActive;

        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật hệ số giá cho '{seatType.TypeName}' thành công!";
        return RedirectToAction(nameof(Index));
    }
}
