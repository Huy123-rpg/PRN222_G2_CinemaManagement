namespace PRN222_CinemaManagement.Repositories.Interfaces;

/// <summary>Trừu tượng hóa giao dịch cơ sở dữ liệu cho tầng Service.</summary>
public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

