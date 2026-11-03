using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Models.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

[SessionAuthorize(Roles = "CinemaManager,Admin")]
public class PricingController(CinemaDbContext context) : Controller
{
    // GET: /Pricing (UC-27 - Quản lý loại ghế & Bảng tính giá vé)
    [HttpGet]
    public async Task<IActionResult> Index(decimal? samplePrice)
    {
        var seatTypes = await context.SeatTypes
            .Include(st => st.Seats)
            .OrderBy(st => st.SeatTypeId)
            .Select(st => new SeatTypeItemViewModel
            {
                SeatTypeId = st.SeatTypeId,
                TypeName = st.TypeName,
                Description = st.Description,
                PriceMultiplier = st.PriceMultiplier,
                IsActive = st.IsActive,
                TotalSeatsInSystem = st.Seats.Count
            })
            .ToListAsync();

        var model = new PricingIndexViewModel
        {
            SeatTypes = seatTypes,
            SampleBasePrice = samplePrice ?? 80000m
        };

        return View(model);
    }

    // GET: /Pricing/EditSeatType/{id}
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

    // POST: /Pricing/EditSeatType/{id} (UC-27 - Thiết lập hệ số giá vé & Cấu hình loại ghế)
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

        string trimmedName = model.TypeName.Trim();
        bool nameExists = await context.SeatTypes.AnyAsync(st => st.SeatTypeId != model.SeatTypeId && st.TypeName.ToLower() == trimmedName.ToLower());
        if (nameExists)
        {
            ModelState.AddModelError("TypeName", "Tên loại ghế đã tồn tại.");
            return View(model);
        }

        seatType.TypeName = trimmedName;
        seatType.Description = model.Description?.Trim();
        seatType.PriceMultiplier = model.PriceMultiplier;
        seatType.IsActive = model.IsActive;

        await context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật loại ghế '{seatType.TypeName}' với hệ số giá {seatType.PriceMultiplier}x thành công!";
        return RedirectToAction(nameof(Index));
    }
}
