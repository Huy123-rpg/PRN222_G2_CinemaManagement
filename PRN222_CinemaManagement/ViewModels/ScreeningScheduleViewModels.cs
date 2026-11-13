using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.ViewModels;

public class ScreeningScheduleViewModel
{
    public DateOnly Date { get; set; }
    public DateOnly SelectedDate { get; set; }
    public int DayCount { get; set; } = 1;
    public string ViewMode { get; set; } = "week";
    public int? AuditoriumId { get; set; }
    public List<AuditoriumOptionViewModel> Auditoriums { get; set; } = [];
    public List<ScreeningItemViewModel> Screenings { get; set; } = [];
}

public class ScreeningItemViewModel
{
    public int ScreeningId { get; set; }
    public int AuditoriumId { get; set; }
    public string MovieTitle { get; set; } = "";
    public string AuditoriumName { get; set; } = "";
    public DateTime StartAtLocal { get; set; }
    public DateTime EndAtLocal { get; set; }
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = "";
    public bool CanEdit { get; set; }
}

public class ScreeningFormViewModel
{
    public int? ScreeningId { get; set; }
    public DateOnly ReturnDate { get; set; }
    public int? ReturnAuditoriumId { get; set; }

    [Required(ErrorMessage = "Chọn phim.")]
    [Range(1, int.MaxValue, ErrorMessage = "Chọn phim.")]
    public int MovieId { get; set; }

    [Required(ErrorMessage = "Chọn phòng chiếu.")]
    [Range(1, int.MaxValue, ErrorMessage = "Chọn phòng chiếu.")]
    public int AuditoriumId { get; set; }

    [Required(ErrorMessage = "Chọn ngày giờ bắt đầu.")]
    [Display(Name = "Bắt đầu lúc (giờ Việt Nam)")]
    public DateTime? StartAtLocal { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Giá cơ bản không được âm.")]
    [Display(Name = "Giá cơ bản")]
    public decimal BasePrice { get; set; }

    public List<MovieOptionViewModel> Movies { get; set; } = [];
    public List<AuditoriumOptionViewModel> Auditoriums { get; set; } = [];
    public List<ScreeningConflictViewModel> Conflicts { get; set; } = [];
    public string? CheckMessage { get; set; }
    public bool CanSave { get; set; } = true;
    public string? ReadOnlyMessage { get; set; }
}

public class MovieOptionViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public DateOnly? ReleaseDate { get; set; }
}

public class AuditoriumOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class ScreeningConflictViewModel
{
    public string MovieTitle { get; set; } = "";
    public string AuditoriumName { get; set; } = "";
    public DateTime StartAtLocal { get; set; }
    public DateTime EndAtLocal { get; set; }
}

public class ScreeningBatchViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn phim.")]
    public int MovieId { get; set; }
    [Required(ErrorMessage = "Chọn ngày bắt đầu.")]
    public DateOnly? StartDate { get; set; }
    [Required(ErrorMessage = "Chọn ngày kết thúc.")]
    public DateOnly? EndDate { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Giá cơ bản không được âm.")]
    public decimal BasePrice { get; set; }
    public string MovieTitle { get; set; } = "";
    public int DurationMinutes { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public List<MovieOptionViewModel> Movies { get; set; } = [];
    public List<AuditoriumOptionViewModel> Auditoriums { get; set; } = [];
    public List<ScreeningBatchSlotViewModel> Slots { get; set; } = [];
    public List<ScreeningBatchConflictViewModel> Conflicts { get; set; } = [];
    public bool ShowPreview { get; set; }
}

public class ScreeningBatchSlotViewModel
{
    public DateOnly? Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public int AuditoriumId { get; set; }
}

public class ScreeningBatchConflictViewModel
{
    public int SlotIndex { get; set; }
    public string Message { get; set; } = "";
    public string? ExistingMovieTitle { get; set; }
    public string? AuditoriumName { get; set; }
    public DateTime? ExistingStartAtLocal { get; set; }
    public DateTime? ExistingEndAtLocal { get; set; }
}
