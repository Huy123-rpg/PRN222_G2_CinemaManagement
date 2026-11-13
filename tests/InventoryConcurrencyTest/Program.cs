using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;

namespace InventoryConcurrencyTest;

/// <summary>
/// KIỂM THỬ TRANH CHẤP TỒN KHO ĐỒNG THỜI (TEC-07)
/// Mô phỏng logic nghiệp vụ của ConcessionController.Sell(), không gọi qua HTTP Controller.
/// </summary>
public static class Program
{
    private static readonly string TestConnectionString = GetSafeTestConnectionString();

    /// <summary>
    /// Bảo vệ Database: Chỉ cho phép duy nhất database 'CinemaDB_Test_TEC07' trên SQL Server cục bộ.
    /// </summary>
    private static string GetSafeTestConnectionString()
    {
        string rawConn = Environment.GetEnvironmentVariable("CINEMA_TEST_SQL")
            ?? "Server=.;Database=CinemaDB_Test_TEC07;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var builder = new SqlConnectionStringBuilder(rawConn);
        string dbName = builder.InitialCatalog.Trim();
        string server = builder.DataSource.Trim();

        // 1. Chỉ chấp nhận chính xác CinemaDB_Test_TEC07 (tuyệt đối không kết nối CinemaDB hay tên khác)
        if (!string.Equals(dbName, "CinemaDB_Test_TEC07", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"[BẢO VỆ DATABASE] Tên database '{dbName}' không hợp lệ! Bài test chỉ được phép chạy trên 'CinemaDB_Test_TEC07'.");
        }

        // 2. Chỉ cho phép SQL Server cục bộ
        bool isLocal = server == "."
            || server.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || server.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || server.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || server.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

        if (!isLocal)
        {
            throw new InvalidOperationException($"[BẢO VỆ SERVER] '{server}' không phải SQL Server cục bộ được phép!");
        }

        return rawConn;
    }

    private static CinemaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseSqlServer(TestConnectionString)
            .Options;
        return new CinemaDbContext(options);
    }

    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BÀI KIỂM THỬ KỸ THUẬT TEC-07: CONCURRENCY ===");
        Console.WriteLine("Database: CinemaDB_Test_TEC07\n");

        try
        {
            int staffId = await InitializeTestDatabaseAsync();

            Console.WriteLine("[KỊCH BẢN 1] Kiểm tra RowVersion ở tầng EF Core (Deterministic)");
            bool s1Passed = await RunScenario1Async();
            Console.WriteLine(s1Passed ? "-> Kịch bản 1: PASSED\n" : "-> Kịch bản 1: FAILED\n");

            Console.WriteLine("[KỊCH BẢN 2] Kiểm thử nghiệp vụ bán hàng đồng thời (True Async Concurrency)");
            Console.WriteLine("Lưu ý: Kịch bản 2 mô phỏng logic nghiệp vụ Sell(), không gọi qua HTTP Controller.");
            bool s2Passed = await RunScenario2Async(staffId);
            Console.WriteLine(s2Passed ? "-> Kịch bản 2: PASSED\n" : "-> Kịch bản 2: FAILED\n");

            Console.WriteLine(s1Passed && s2Passed ? "=== TỔNG KẾT: PASSED ===" : "=== TỔNG KẾT: FAILED ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LỖI TEST] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<int> InitializeTestDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var staff = await db.Staffs.FirstOrDefaultAsync();
        if (staff != null) return staff.StaffId;

        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleId == 3);
        if (role == null)
        {
            role = new Role { RoleId = 3, RoleName = "Staff", Description = "Test Staff Role" };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            Username = "test_staff_tec07",
            Email = "tec07@cinema.local",
            PasswordHash = "dummy_hash",
            FullName = "Staff Test TEC07",
            RoleId = 3,
            Status = "Active"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var newStaff = new Staff { UserId = user.UserId, StaffCode = "STF_TEC07" };
        db.Staffs.Add(newStaff);
        await db.SaveChangesAsync();

        return newStaff.StaffId;
    }

    /// <summary>
    /// Kịch bản 1: 2 DbContext độc lập cùng nạp 1 món (Stock=3). DbContext 1 trừ 2 và lưu trước.
    /// DbContext 2 trừ 2 và lưu sau -> Bắt buộc bị chặn bởi DbUpdateConcurrencyException.
    /// </summary>
    private static async Task<bool> RunScenario1Async()
    {
        await using var dbSetup = CreateDbContext();
        var item = new ConcessionItem
        {
            Name = "Bắp Test S1 (" + Guid.NewGuid().ToString("N")[..6] + ")",
            Price = 50000m,
            StockQuantity = 3,
            LowStockThreshold = 1,
            Status = "Active"
        };
        dbSetup.ConcessionItems.Add(item);
        await dbSetup.SaveChangesAsync();

        await using var db1 = CreateDbContext();
        await using var db2 = CreateDbContext();

        var item1 = await db1.ConcessionItems.FindAsync(item.ConcessionItemId);
        var item2 = await db2.ConcessionItems.FindAsync(item.ConcessionItemId);

        item1!.StockQuantity -= 2;
        await db1.SaveChangesAsync();

        item2!.StockQuantity -= 2;
        bool caughtConcurrency = false;

        try
        {
            await db2.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            caughtConcurrency = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi không mong đợi ở Kịch bản 1: " + ex.Message);
        }

        await using var dbVerify = CreateDbContext();
        var finalItem = await dbVerify.ConcessionItems.FindAsync(item.ConcessionItemId);

        return caughtConcurrency && finalItem?.StockQuantity == 1;
    }

    /// <summary>
    /// Kịch bản 2: Mô phỏng 2 giao dịch đồng thời từ 2 thu ngân (mỗi bên mua 2 phần, kho ban đầu = 3).
    /// Sử dụng điểm đồng bộ để cả 2 bên chắc chắn đọc xong Stock & RowVersion trước khi ghi.
    /// Có cơ chế timeout 5s để tránh chờ vô hạn nếu 1 Task bị lỗi sớm.
    /// </summary>
    private static async Task<bool> RunScenario2Async(int staffId)
    {
        await using var dbSetup = CreateDbContext();
        var testItem = new ConcessionItem
        {
            Name = "Nước Test S2 (" + Guid.NewGuid().ToString("N")[..6] + ")",
            Price = 30000m,
            StockQuantity = 3,
            LowStockThreshold = 1,
            Status = "Active"
        };
        dbSetup.ConcessionItems.Add(testItem);
        await dbSetup.SaveChangesAsync();
        int itemId = testItem.ConcessionItemId;

        // Điểm đồng bộ: Đảm bảo cả 2 luồng đọc xong trước khi trừ kho
        var readyTrigger = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int readCount = 0;

        async Task<SaleResult> SimulateSellTransactionAsync(string cashierName, int buyQty)
        {
            await using var db = CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                // Bước 1: Đọc mặt hàng
                var item = await db.ConcessionItems.FindAsync(itemId);
                if (item == null || item.Status != "Active")
                {
                    await tx.RollbackAsync();
                    return new SaleResult(cashierName, false, false, "Mặt hàng không khả dụng");
                }

                // Bước 2: Đồng bộ - Báo hiệu đã đọc xong StockQuantity & RowVersion ban đầu
                if (Interlocked.Increment(ref readCount) == 2)
                {
                    readyTrigger.TrySetResult(true);
                }

                // Chờ luồng còn lại đọc xong, tối đa 5 giây tránh treo vô hạn
                var waitResult = await Task.WhenAny(readyTrigger.Task, Task.Delay(5000));
                if (waitResult != readyTrigger.Task)
                {
                    await tx.RollbackAsync();
                    return new SaleResult(cashierName, false, false, "Timeout đồng bộ (luồng kia không kịp đọc)");
                }

                // Bước 3: Kiểm tra tồn kho
                if (item.StockQuantity < buyQty)
                {
                    await tx.RollbackAsync();
                    return new SaleResult(cashierName, false, false, "Không đủ tồn kho");
                }

                // Bước 4: Trừ kho và tạo hóa đơn
                item.StockQuantity -= buyQty;

                var sale = new ConcessionSale
                {
                    SaleCode = "S2-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    StaffId = staffId,
                    SaleDate = DateTime.UtcNow,
                    TotalAmount = buyQty * item.Price,
                    PaymentMethod = "Cash",
                    Status = "Completed"
                };

                sale.ConcessionSaleItems.Add(new ConcessionSaleItem
                {
                    ConcessionItemId = itemId,
                    Quantity = buyQty,
                    UnitPrice = item.Price
                });

                db.ConcessionSales.Add(sale);
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                return new SaleResult(cashierName, true, false, $"Thành công (Đơn {sale.SaleCode})");
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                return new SaleResult(cashierName, false, true, "Bị chặn bởi DbUpdateConcurrencyException (Đã Rollback)");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new SaleResult(cashierName, false, false, $"Lỗi không mong đợi ({ex.GetType().Name}): {ex.Message}");
            }
        }

        // Chạy 2 giao dịch đồng thời
        var taskA = Task.Run(() => SimulateSellTransactionAsync("Thu Ngân A", 2));
        var taskB = Task.Run(() => SimulateSellTransactionAsync("Thu Ngân B", 2));

        var results = await Task.WhenAll(taskA, taskB);

        foreach (var r in results)
        {
            Console.WriteLine($"  [{r.Cashier}] {r.Message}");
        }

        // Kiểm chứng tính toàn vẹn trong Database bằng DbContext thứ ba
        await using var dbVerify = CreateDbContext();
        var itemAfter = await dbVerify.ConcessionItems.FindAsync(itemId);
        var salesCount = await dbVerify.ConcessionSaleItems.CountAsync(si => si.ConcessionItemId == itemId);

        Console.WriteLine($"  -> Tồn kho thực tế: {itemAfter?.StockQuantity} (Kỳ vọng: 1)");
        Console.WriteLine($"  -> Hóa đơn ghi nhận: {salesCount} (Kỳ vọng: 1)");

        bool oneSuccess = results.Count(r => r.Success) == 1;
        bool oneBlocked = results.Count(r => r.IsConcurrencyBlocked) == 1;
        bool noUnexpectedErrors = results.All(r => r.Success || r.IsConcurrencyBlocked);
        bool stockCorrect = itemAfter?.StockQuantity == 1;
        bool invoiceCorrect = salesCount == 1;

        return oneSuccess && oneBlocked && noUnexpectedErrors && stockCorrect && invoiceCorrect;
    }

    private record SaleResult(string Cashier, bool Success, bool IsConcurrencyBlocked, string Message);
}
