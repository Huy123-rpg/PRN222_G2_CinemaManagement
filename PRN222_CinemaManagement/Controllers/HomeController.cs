using Microsoft.AspNetCore.Mvc;
using PRN222_CinemaManagement.Models;
using PRN222_CinemaManagement.Services.IService;
using PRN222_CinemaManagement.ViewModels;
using System.Diagnostics;

namespace PRN222_CinemaManagement.Controllers
{
    public class HomeController(IMovieService movieService) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var nowShowing = await movieService.GetMovieListAsync("showing", null, null);
            var upcoming = await movieService.GetMovieListAsync("upcoming", null, null);
            return View(new HomePageViewModel
            {
                NowShowing = nowShowing.Movies.Take(4).ToList(),
                Upcoming = upcoming.Movies.Take(4).ToList()
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
