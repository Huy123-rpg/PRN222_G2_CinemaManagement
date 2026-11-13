namespace PRN222_CinemaManagement.ViewModels;

public class HomePageViewModel
{
    public List<MovieItemViewModel> NowShowing { get; set; } = [];
    public List<MovieItemViewModel> Upcoming { get; set; } = [];
}
