using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Filters;

namespace PRN222_CinemaManagement.Controllers;

[SessionAuthorize(Roles = "Staff,CinemaManager,Admin")]
public class DashboardController : Controller
{
    public IActionResult Index() => View();
}
