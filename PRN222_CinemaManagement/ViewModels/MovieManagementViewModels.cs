using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.ViewModels;

public class CreateMovieViewModel
{
    [Required, StringLength(200)]
    [Display(Name = "Tên phim")]
    public string Title { get; set; } = "";

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(1, 1000)]
    [Display(Name = "Thời lượng (phút)")]
    public int DurationMinutes { get; set; }

    [Required]
    [RegularExpression("^(P|K|T13|T16|T18)$", ErrorMessage = "Chọn mức phân loại hợp lệ.")]
    [Display(Name = "Phân loại tuổi")]
    public string AgeRating { get; set; } = "P";

    [Required(ErrorMessage = "Nhập ngày khởi chiếu để lập lịch.")]
    [Display(Name = "Ngày khởi chiếu")]
    public DateOnly? ReleaseDate { get; set; }

    [Required, Url, StringLength(1000)]
    [Display(Name = "URL poster")]
    public string PosterUrl { get; set; } = "";

    [Url, StringLength(1000)]
    [Display(Name = "URL trailer (không bắt buộc)")]
    public string? TrailerUrl { get; set; }

    [Display(Name = "Thể loại")]
    public List<int> GenreIds { get; set; } = [];

    public List<GenreItemViewModel> Genres { get; set; } = [];
}

public class EditMovieViewModel
{
    public int MovieId { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Tên phim")]
    public string Title { get; set; } = "";

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^(P|K|T13|T16|T18)$", ErrorMessage = "Chọn mức phân loại hợp lệ.")]
    [Display(Name = "Phân loại tuổi")]
    public string AgeRating { get; set; } = "P";

    [Required(ErrorMessage = "Nhập ngày khởi chiếu để lập lịch.")]
    [Display(Name = "Ngày khởi chiếu")]
    public DateOnly? ReleaseDate { get; set; }

    [Required, Url, StringLength(1000)]
    [Display(Name = "URL poster")]
    public string PosterUrl { get; set; } = "";

    [Url, StringLength(1000)]
    [Display(Name = "URL trailer (không bắt buộc)")]
    public string? TrailerUrl { get; set; }

    [Display(Name = "Thể loại")]
    public List<int> GenreIds { get; set; } = [];

    public List<GenreItemViewModel> Genres { get; set; } = [];
}
