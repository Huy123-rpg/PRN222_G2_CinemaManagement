namespace PRN222_CinemaManagement.Services;

/// <summary>Mỗi lần quét tạo scope mới vì DbContext không được dùng chung giữa các luồng.</summary>
public class SeatHoldExpirationService(IServiceScopeFactory scopeFactory,
    ILogger<SeatHoldExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IOnlineBookingService>().ReleaseExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Lỗi tạm thời không làm chết worker; lượt quét sau tiếp tục dọn các đơn còn hạn hết.
                logger.LogError(ex, "Không thể giải phóng ghế hết hạn trong lượt quét này.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
