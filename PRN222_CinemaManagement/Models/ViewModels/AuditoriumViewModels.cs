using System.ComponentModel.DataAnnotations;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Models.ViewModels;

// Dùng cho màn hình tạo mới phòng chiếu (UC-25)
public class AuditoriumCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên phòng chiếu")]
    [Display(Name = "Tên phòng chiếu")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số hàng ghế")]
    [Range(1, 26, ErrorMessage = "Số hàng ghế từ 1 đến 26 (tương ứng từ hàng A đến Z)")]
    [Display(Name = "Số hàng ghế")]
    public int TotalRows { get; set; } = 8;

    [Required(ErrorMessage = "Vui lòng nhập số ghế mỗi hàng")]
    [Range(1, 20, ErrorMessage = "Số ghế mỗi hàng từ 1 đến 20")]
    [Display(Name = "Số ghế mỗi hàng")]
    public int TotalColumns { get; set; } = 10;

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Active";
}

// Dùng cho màn hình chỉnh sửa thông tin phòng chiếu (UC-25)
public class AuditoriumEditViewModel
{
    public int AuditoriumId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên phòng chiếu")]
    [Display(Name = "Tên phòng chiếu")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Active";

    [Display(Name = "Số hàng")]
    public int TotalRows { get; set; }

    [Display(Name = "Số cột")]
    public int TotalColumns { get; set; }
}

// Dùng cho màn hình xem & cấu hình sơ đồ ghế (UC-25, UC-26)
public class ConfigureSeatsViewModel
{
    public Auditorium Auditorium { get; set; } = null!;
    public List<SeatType> SeatTypes { get; set; } = new();
}
