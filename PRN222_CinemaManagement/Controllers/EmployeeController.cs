using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222_CinemaManagement.Filters;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;

namespace PRN222_CinemaManagement.Controllers;

[SessionAuthorize(Roles = "Admin")]
public class EmployeeController(IEmployeeService employeeService) : Controller
{
    // GET: /Employee (UC-31)
    [HttpGet]
    public async Task<IActionResult> Index(string? searchKeyword, string? roleFilter, string? statusFilter)
    {
        var viewModel = await employeeService.GetEmployeeListAsync(searchKeyword, roleFilter, statusFilter);
        ViewBag.Roles = await employeeService.GetEmployeeFilterRoleNamesAsync();

        return View(viewModel);
    }

    // GET: /Employee/Create (UC-32)
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadRoleSelectListAsync();
        return View(new CreateEmployeeViewModel());
    }

    // POST: /Employee/Create (UC-32)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmployeeViewModel model)
    {
        var result = await employeeService.CreateEmployeeAsync(model, ModelState.IsValid);
        if (!result.Succeeded)
        {
            AddErrorsToModelState(result);
            await LoadRoleSelectListAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // GET: /Employee/Edit/5 (UC-33 & UC-34)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await employeeService.GetEmployeeForEditAsync(id);
        if (vm == null)
        {
            return NotFound();
        }

        await LoadRoleSelectListAsync(vm.RoleId);
        return View(vm);
    }

    // POST: /Employee/Edit/5 (UC-33 & UC-34)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditEmployeeViewModel model)
    {
        if (id != model.UserId) return BadRequest();

        var result = await employeeService.UpdateEmployeeAsync(id, model, ModelState.IsValid);
        if (result.IsNotFound) return NotFound();

        if (!result.Succeeded)
        {
            AddErrorsToModelState(result);
            await LoadRoleSelectListAsync(model.RoleId);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // POST: /Employee/ToggleStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var result = await employeeService.ToggleEmployeeStatusAsync(id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message
                ?? "Không thể thay đổi trạng thái của tài khoản này.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private void AddErrorsToModelState(ServiceResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }
    }

    private async Task LoadRoleSelectListAsync(int? selectedRoleId = null)
    {
        var roles = await employeeService.GetAssignableRolesAsync();
        ViewBag.Roles = new SelectList(roles, "RoleId", "RoleName", selectedRoleId);
    }
}
