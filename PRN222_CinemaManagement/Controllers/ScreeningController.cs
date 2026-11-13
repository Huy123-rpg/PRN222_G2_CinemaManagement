using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

[SessionAuthorize(Roles = "CinemaManager,Admin")]
public class ScreeningController(IScreeningScheduleService scheduleService) : Controller
{
    private static TimeZoneInfo VietnamTimeZone => TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date, int? auditoriumId, string? view)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));
        var selectedDate = date ?? today;
        var isWeek = view != "day";
        var periodStart = isWeek ? selectedDate.AddDays(-(((int)selectedDate.DayOfWeek + 6) % 7)) : selectedDate;
        var model = await scheduleService.GetScheduleAsync(periodStart, auditoriumId, isWeek ? 7 : 1);
        model.SelectedDate = selectedDate;
        model.ViewMode = isWeek ? "week" : "day";
        ViewBag.TodayDate = today;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(DateOnly? date, int? auditoriumId)
    {
        var model = await scheduleService.GetCreateFormAsync();
        model.ReturnDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));
        model.ReturnAuditoriumId = auditoriumId;
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateBatch(int? movieId, CancellationToken cancellationToken)
    {
        ScreeningBatchViewModel model;
        try
        {
            model = await scheduleService.GetBatchFormAsync(movieId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        if (model.MovieId == 0)
        {
            TempData["ErrorMessage"] = "Cần có phim sắp chiếu hoặc đang chiếu trước khi lập lịch.";
            return RedirectToAction("Index", "Movie", new { tab = "upcoming" });
        }
        return View("Batch", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBatch(ScreeningBatchViewModel model, string command, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                if (command == "preview")
                {
                    model.Conflicts = await scheduleService.CheckBatchConflictsAsync(model, cancellationToken);
                    model.ShowPreview = true;
                    if (model.Conflicts.Count > 0)
                        ModelState.AddModelError(string.Empty, $"Có {model.Conflicts.Select(c => c.SlotIndex).Distinct().Count()} suất cần chỉnh do xung đột lịch.");
                }
                else
                {
                    await scheduleService.CreateBatchAsync(model, cancellationToken);
                    TempData["SuccessMessage"] = $"Đã tạo {model.Slots.Count} suất chiếu và sơ đồ ghế.";
                    return RedirectToAction(nameof(Index), new { date = model.StartDate });
                }
            }
            catch (ScreeningBatchException ex)
            {
                model.Conflicts = ex.Conflicts;
                model.ShowPreview = true;
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await PopulateBatchOptionsAsync(model, cancellationToken);
        return View("Batch", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScreeningFormViewModel model, bool checkOnly = false)
    {
        model.ScreeningId = null;
        if (ModelState.IsValid)
        {
            if (checkOnly)
            {
                await CheckConflictsAsync(model);
                await PopulateOptionsAsync(model);
                return View("Form", model);
            }

            try
            {
                await scheduleService.CreateAsync(ToRequest(model));
                TempData["SuccessMessage"] = "Đã tạo suất chiếu và sơ đồ ghế.";
                return RedirectToAction(nameof(Index), new { date = model.ReturnDate, auditoriumId = model.ReturnAuditoriumId });
            }
            catch (ScreeningScheduleException ex)
            {
                model.Conflicts = ex.Conflicts;
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await PopulateOptionsAsync(model);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, DateOnly? date, int? auditoriumId)
    {
        var model = await scheduleService.GetEditFormAsync(id);
        if (model == null) return NotFound();
        model.ReturnDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));
        model.ReturnAuditoriumId = auditoriumId;
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScreeningFormViewModel model, bool checkOnly = false)
    {
        model.ScreeningId = id;
        if (ModelState.IsValid)
        {
            if (checkOnly)
            {
                await CheckConflictsAsync(model);
                await PopulateOptionsAsync(model);
                return View("Form", model);
            }

            try
            {
                await scheduleService.UpdateAsync(id, ToRequest(model));
                TempData["SuccessMessage"] = "Đã cập nhật lịch chiếu và giá ghế.";
                return RedirectToAction(nameof(Index), new { date = model.ReturnDate, auditoriumId = model.ReturnAuditoriumId });
            }
            catch (ScreeningScheduleException ex)
            {
                model.Conflicts = ex.Conflicts;
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await PopulateOptionsAsync(model);
        return View("Form", model);
    }

    private async Task CheckConflictsAsync(ScreeningFormViewModel model)
    {
        try
        {
            model.Conflicts = await scheduleService.CheckConflictsAsync(model);
            model.CheckMessage = model.Conflicts.Count == 0
                ? "Không tìm thấy lịch trùng trong phòng đã chọn."
                : "Phát hiện các suất chiếu có thể bị trùng:";
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
    }

    private async Task PopulateOptionsAsync(ScreeningFormViewModel model)
    {
        var options = await scheduleService.GetCreateFormAsync();
        model.Movies = options.Movies;
        model.Auditoriums = options.Auditoriums;
    }

    private async Task PopulateBatchOptionsAsync(ScreeningBatchViewModel model, CancellationToken cancellationToken)
    {
        ScreeningBatchViewModel options;
        try
        {
            options = await scheduleService.GetBatchFormAsync(model.MovieId > 0 ? model.MovieId : null, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            options = await scheduleService.GetBatchFormAsync(null, cancellationToken);
        }
        model.Movies = options.Movies;
        model.Auditoriums = options.Auditoriums;
        var selectedMovie = model.Movies.FirstOrDefault(m => m.Id == model.MovieId);
        model.MovieTitle = selectedMovie?.Title ?? options.MovieTitle;
        model.DurationMinutes = selectedMovie?.DurationMinutes ?? options.DurationMinutes;
        model.ReleaseDate = selectedMovie?.ReleaseDate ?? options.ReleaseDate;
    }

    private static SaveScreeningRequest ToRequest(ScreeningFormViewModel model) =>
        new(model.MovieId, model.AuditoriumId, model.StartAtLocal!.Value, model.BasePrice);
}
