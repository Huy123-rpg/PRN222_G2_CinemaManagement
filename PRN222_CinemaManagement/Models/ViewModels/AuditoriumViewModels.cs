using System.ComponentModel.DataAnnotations;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Models.ViewModels;

public class AuditoriumListItemViewModel
{
    public int AuditoriumId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public int TotalSeats { get; set; }
    public int ActiveSeatsCount { get; set; }
    public int VipSeatsCount { get; set; }
    public int CoupleSeatsCount { get; set; }
    public string Status { get; set; } = "Active";
    public bool HasShowtimes { get; set; }
}

public class AuditoriumCreateViewModel
{
    [Required(ErrorMessage = "Tên phòng chiếu không được để trống")]
    [StringLength(100, ErrorMessage = "Tên phòng chiếu tối đa 100 ký tự")]
    [Display(Name = "Tên phòng chiếu")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số hàng ghế")]
    [Range(1, 26, ErrorMessage = "Số hàng ghế phải từ 1 đến 26 (tương ứng hàng A đến Z)")]
    [Display(Name = "Số hàng ghế (Rows)")]
    public int TotalRows { get; set; } = 8;

    [Required(ErrorMessage = "Vui lòng nhập số ghế mỗi hàng")]
    [Range(1, 30, ErrorMessage = "Số ghế mỗi hàng phải từ 1 đến 30")]
    [Display(Name = "Số ghế mỗi hàng (Columns)")]
    public int TotalColumns { get; set; } = 10;

    [Display(Name = "Trạng thái hoạt động")]
    public string Status { get; set; } = "Active";

    [Display(Name = "Loại ghế mặc định")]
    public int DefaultSeatTypeId { get; set; } = 1;
}

public class AuditoriumEditViewModel
{
    public int AuditoriumId { get; set; }

    [Required(ErrorMessage = "Tên phòng chiếu không được để trống")]
    [StringLength(100, ErrorMessage = "Tên phòng chiếu tối đa 100 ký tự")]
    [Display(Name = "Tên phòng chiếu")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Số hàng ghế")]
    public int TotalRows { get; set; }

    [Display(Name = "Số cột ghế")]
    public int TotalColumns { get; set; }

    [Display(Name = "Trạng thái hoạt động")]
    public string Status { get; set; } = "Active";

    public bool HasShowtimes { get; set; }
}

public class SeatItemViewModel
{
    public int SeatId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public int SeatTypeId { get; set; }
    public string SeatTypeName { get; set; } = string.Empty;
    public decimal PriceMultiplier { get; set; }
    public string Status { get; set; } = "Active"; // Active, Broken, Inactive
}

public class AuditoriumSeatConfigViewModel
{
    public int AuditoriumId { get; set; }
    public string AuditoriumName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public string Status { get; set; } = string.Empty;

    public List<SeatType> AvailableSeatTypes { get; set; } = new();
    public List<SeatItemViewModel> Seats { get; set; } = new();

    // Thống kê nhanh
    public int TotalSeats => Seats.Count;
    public int StandardCount => Seats.Count(s => s.SeatTypeName == "Standard" && s.Status == "Active");
    public int VipCount => Seats.Count(s => s.SeatTypeName == "VIP" && s.Status == "Active");
    public int CoupleCount => Seats.Count(s => s.SeatTypeName == "Couple" && s.Status == "Active");
    public int BrokenCount => Seats.Count(s => s.Status == "Broken");
}

public class UpdateSeatsRequestModel
{
    public int AuditoriumId { get; set; }
    public List<int> SeatIds { get; set; } = new();
    public int? SeatTypeId { get; set; }
    public string? Status { get; set; }
}
