using System.ComponentModel.DataAnnotations;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Models.ViewModels;

public class SeatTypeItemViewModel
{
    public int SeatTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceMultiplier { get; set; }
    public bool IsActive { get; set; }
    public int TotalSeatsInSystem { get; set; }
}

public class PricingIndexViewModel
{
    public List<SeatTypeItemViewModel> SeatTypes { get; set; } = new();

    // Mẫu giá tính toán thử nghiệm
    [Display(Name = "Giá vé cơ sở mô phỏng (VNĐ)")]
    public decimal SampleBasePrice { get; set; } = 80000m;
}

public class EditSeatTypeViewModel
{
    public int SeatTypeId { get; set; }

    [Required(ErrorMessage = "Tên loại ghế không được để trống")]
    [StringLength(50, ErrorMessage = "Tên loại ghế tối đa 50 ký tự")]
    [Display(Name = "Tên loại ghế")]
    public string TypeName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập hệ số giá")]
    [Range(0.1, 10.0, ErrorMessage = "Hệ số giá phải lớn hơn 0 và tối đa 10.0")]
    [Display(Name = "Hệ số giá (Price Multiplier)")]
    public decimal PriceMultiplier { get; set; } = 1.000m;

    [Display(Name = "Đang kích hoạt")]
    public bool IsActive { get; set; } = true;
}
