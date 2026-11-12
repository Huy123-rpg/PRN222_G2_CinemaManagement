using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

public class MovieController(IMovieService movieService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? tab, string? search, int? genreId)
    {
        var role = HttpContext.Session.GetString("Role");
        if (tab == "ended" && role != "Admin") tab = "showing";
        var model = await movieService.GetMovieListAsync(tab, search, genreId);
        model.CanManage = role is "Admin" or "CinemaManager";
        model.CanReverseStatus = role == "Admin";
        return View(model);
    }

    [HttpGet, SessionAuthorize(Roles = "CinemaManager,Admin")]
    public async Task<IActionResult> Create() => View(await movieService.GetCreateMovieModelAsync());

    [HttpPost, ValidateAntiForgeryToken, SessionAuthorize(Roles = "CinemaManager,Admin")]
    public async Task<IActionResult> Create(CreateMovieViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await movieService.CreateMovieAsync(model);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("CreateBatch", "Screening", new { movieId = result.Data });
            }
            AddErrors(result);
        }

        model.Genres = await movieService.GetGenresAsync();
        return View(model);
    }

    [HttpGet, SessionAuthorize(Roles = "CinemaManager,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await movieService.GetEditMovieModelAsync(id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, SessionAuthorize(Roles = "CinemaManager,Admin")]
    public async Task<IActionResult> Edit(int id, EditMovieViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await movieService.UpdateMovieAsync(id, model);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Index));
            }
            if (result.IsNotFound) return NotFound();
            AddErrors(result);
        }

        model.Genres = await movieService.GetGenresAsync();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, SessionAuthorize(Roles = "CinemaManager,Admin")]
    public async Task<IActionResult> ChangeStatus(int id, string newStatus, string? tab, string? search, int? genreId)
    {
        var result = await movieService.ChangeStatusAsync(
            id, newStatus, HttpContext.Session.GetString("Role"));

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? result.Message : result.IsNotFound ? "Không tìm thấy phim." : result.Errors.FirstOrDefault()?.Message;

        return RedirectToAction(nameof(Index), new { tab, search, genreId });
    }

    private void AddErrors(ServiceResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Field, error.Message);
    }
}
