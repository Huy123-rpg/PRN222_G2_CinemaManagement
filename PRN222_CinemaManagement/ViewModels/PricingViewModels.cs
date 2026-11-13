using System.ComponentModel.DataAnnotations;

namespace PRN222_CinemaManagement.ViewModels;

// Dùng cho màn hình chỉnh sửa hệ số giá loại ghế (UC-27)
public class EditSeatTypeViewModel
{
    public int SeatTypeId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên loại ghế")]
    [Display(Name = "Tên loại ghế")]
    public string TypeName { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập hệ số giá")]
    [Range(0.1, 5.0, ErrorMessage = "Hệ số giá phải từ 0.1 đến 5.0")]
    [Display(Name = "Hệ số nhân giá (ví dụ: 1.0, 1.2)")]
    public decimal PriceMultiplier { get; set; } = 1.0m;

    [Display(Name = "Trạng thái kích hoạt")]
    public bool IsActive { get; set; } = true;
}
