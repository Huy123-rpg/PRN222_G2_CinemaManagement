using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>
/// Implementation of <see cref="IConcessionService"/>.
/// Handles sales validation, stock deduction, and concurrency transactions.
/// </summary>
public class ConcessionService(IConcessionRepository concessionRepository) : IConcessionService
{
    public Task<List<ConcessionItem>> GetActiveItemsAsync() =>
        concessionRepository.GetActiveItemsAsync();

    public Task<(List<ConcessionItem> Items, int TotalCount)> GetActiveItemsPagedAsync(int page, int pageSize) =>
        concessionRepository.GetActiveItemsPagedAsync(page, pageSize);

    public Task<List<ConcessionItem>> GetLowStockItemsAsync() =>
        concessionRepository.GetLowStockItemsAsync();

    public Task<(List<ConcessionItem> Items, int TotalCount, int OutOfStockCount, int LowStockCount)> GetLowStockItemsPagedAsync(int page, int pageSize) =>
        concessionRepository.GetLowStockItemsPagedAsync(page, pageSize);

    public Task<ConcessionSale?> GetSaleDetailsAsync(int saleId) =>
        concessionRepository.GetSaleByIdAsync(saleId);

    public async Task<ServiceResult<ConcessionSale>> SellAsync(SellConcessionRequest request)
    {
        // 1. Xác thực nhân viên thu ngân
        var staff = await concessionRepository.GetStaffByUserIdAsync(request.StaffUserId);
        if (staff == null)
        {
            return ServiceResult<ConcessionSale>.Fail(string.Empty, 
                "Tài khoản hiện tại không được gắn với hồ sơ nhân viên thu ngân. Không thể thực hiện giao dịch.");
        }

        // 2. Kiểm tra phương thức thanh toán hợp lệ (Chỉ chấp nhận Tiền mặt hoặc Giả lập VNPay tại quầy)
        var validPaymentMethods = new[] { "Cash", "VNPay_Simulated" };
        if (string.IsNullOrWhiteSpace(request.PaymentMethod) || 
            !validPaymentMethods.Contains(request.PaymentMethod.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return ServiceResult<ConcessionSale>.Fail(string.Empty, "Phương thức thanh toán không hợp lệ.");
        }

        // 3. Kiểm tra danh sách món hàng gửi lên
        if (request.Items == null || request.Items.Count == 0)
        {
            return ServiceResult<ConcessionSale>.Fail(string.Empty, "Vui lòng chọn ít nhất một món ăn/uống để bán.");
        }

        // Chống gửi trùng lặp ID món trong cùng một đơn
        if (request.Items.GroupBy(x => x.ItemId).Any(g => g.Count() > 1))
        {
            return ServiceResult<ConcessionSale>.Fail(string.Empty, "Dữ liệu không hợp lệ: phát hiện mặt hàng bị gửi trùng lặp.");
        }

        // Từ chối nếu có bất kỳ dòng sản phẩm nào có số lượng <= 0 (không âm thầm bỏ qua)
        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return ServiceResult<ConcessionSale>.Fail(string.Empty, "Số lượng bán của mỗi món phải lớn hơn 0.");
        }

        // 4. Tải các mặt hàng từ database để kiểm tra trạng thái và tồn kho
        var itemIds = request.Items.Select(x => x.ItemId).ToList();
        var dbItems = await concessionRepository.GetItemsByIdsAsync(itemIds);

        if (dbItems.Count != itemIds.Count)
        {
            return ServiceResult<ConcessionSale>.Fail(string.Empty, "Một số mặt hàng đã chọn không tồn tại trong hệ thống.");
        }

        foreach (var entry in request.Items)
        {
            var dbItem = dbItems.First(c => c.ConcessionItemId == entry.ItemId);

            if (dbItem.Status != "Active")
            {
                return ServiceResult<ConcessionSale>.Fail(string.Empty, $"Món '{dbItem.Name}' hiện đã ngừng kinh doanh.");
            }

            if (dbItem.StockQuantity < entry.Quantity)
            {
                return ServiceResult<ConcessionSale>.Fail(string.Empty, 
                    $"Món '{dbItem.Name}' chỉ còn {dbItem.StockQuantity} phần trong kho, không đủ để bán ({entry.Quantity} phần).");
            }
        }

        // 5. Mở Transaction: trừ tồn kho và tạo hóa đơn bán hàng cùng lúc
        ConcessionSale? createdSale = null;
        try
        {
            await concessionRepository.ExecuteInTransactionAsync(async () =>
            {
                var sale = new ConcessionSale
                {
                    SaleCode = "FNB" + DateTime.UtcNow.ToString("yyMMddHHmmss") + Guid.NewGuid().ToString("N")[..4].ToUpper(),
                    StaffId = staff.StaffId,
                    SaleDate = DateTime.UtcNow,
                    PaymentMethod = request.PaymentMethod.Trim(),
                    Status = "Completed"
                };

                decimal totalAmount = 0m;
                foreach (var entry in request.Items)
                {
                    var dbItem = dbItems.First(c => c.ConcessionItemId == entry.ItemId);

                    // Trừ tồn kho (UC-16)
                    dbItem.StockQuantity -= entry.Quantity;

                    // Tính tiền dựa trên giá gốc từ Database (không tin giá form gửi lên)
                    decimal lineTotal = entry.Quantity * dbItem.Price;
                    totalAmount += lineTotal;

                    sale.ConcessionSaleItems.Add(new ConcessionSaleItem
                    {
                        ConcessionItemId = dbItem.ConcessionItemId,
                        Quantity = entry.Quantity,
                        UnitPrice = dbItem.Price
                    });
                }

                sale.TotalAmount = totalAmount;
                concessionRepository.AddSale(sale);
                await concessionRepository.SaveChangesAsync();
                createdSale = sale;
            });

            return ServiceResult<ConcessionSale>.Success(createdSale!, $"Bán bắp nước thành công! Mã đơn: {createdSale!.SaleCode}.");
        }
        catch (DbUpdateConcurrencyException)
        {
            // Bắt xung đột khi hai nhân viên cùng bán món hàng cuối cùng (TEC-07)
            return ServiceResult<ConcessionSale>.Fail(string.Empty, 
                "Dữ liệu tồn kho vừa được cập nhật bởi một giao dịch khác. Vui lòng tải lại trang và thử lại.");
        }
        catch (Exception)
        {
            // Bảo mật: Không để lộ chi tiết lỗi SQL hay stack trace ra ngoài UI
            return ServiceResult<ConcessionSale>.Fail(string.Empty, 
                "Có lỗi xảy ra trong quá trình xử lý giao dịch. Vui lòng thử lại sau.");
        }
    }
}
