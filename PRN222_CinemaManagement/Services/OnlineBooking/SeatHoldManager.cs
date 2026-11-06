using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Data;

namespace PRN222_CinemaManagement.Services.OnlineBooking;

/// <summary>
/// Tầng thao tác trên ghế của một suất chiếu.
/// Mọi thay đổi trạng thái ghế đều đi qua câu lệnh UPDATE nguyên tử (ExecuteUpdateAsync)
/// với điều kiện lọc nằm ngay trong SQL, nhằm chống race condition khi hai khách
/// cùng chọn một ghế tại cùng một thời điểm (TEC-02 của đặc tả).
/// </summary>
public sealed class SeatHoldManager(CinemaDbContext db)
{
    /// <summary>
    /// Thử giữ một ghế cho một đơn. Trả về false nếu ghế đã bị người khác giữ/đã bán.
    /// Điều kiện nằm trong chính câu UPDATE nên hai request chạy song song chỉ một bên thành công;
    /// bên còn lại nhận 0 dòng bị ảnh hưởng và phải rollback toàn bộ lựa chọn.
    /// </summary>
    public async Task<bool> TryAcquireAsync(int showtimeId, int showtimeSeatId, int bookingId,
        DateTime expiresAt, DateTime now, CancellationToken ct)
    {
        // Chỉ nhận ghế đang trống, hoặc ghế đang giữ nhưng đã hết hạn (worker chưa kịp giải phóng).
        // Tuyệt đối không lấy lại ghế Sold/Blocked và không giành ghế của chính đơn đang xét.
        var affected = await db.ShowtimeSeats
            .Where(s => s.ShowtimeSeatId == showtimeSeatId
                     && s.ShowtimeId == showtimeId
                     && (s.Status == BookingStates.SeatAvailable
                         || (s.Status == BookingStates.SeatHeld && s.HoldExpiresAt != null && s.HoldExpiresAt <= now))
                     && (s.HeldByBookingId == null || s.HeldByBookingId != bookingId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingStates.SeatHeld)
                .SetProperty(s => s.HeldByBookingId, (int?)bookingId)
                .SetProperty(s => s.HoldExpiresAt, expiresAt), ct);

        return affected > 0;
    }

    /// <summary>
    /// Giải phóng các ghế đang do đơn giữ, trừ những ghế trong <paramref name="keepSeatIds"/>.
    /// Dùng cho thao tác bỏ chọn / thay đổi lựa chọn ghế.
    /// </summary>
    public async Task<int> ReleaseAsync(int bookingId, IReadOnlyCollection<int> keepSeatIds, CancellationToken ct)
    {
        var query = db.ShowtimeSeats.Where(s => s.HeldByBookingId == bookingId && s.Status == BookingStates.SeatHeld);
        if (keepSeatIds.Count > 0)
            query = query.Where(s => !keepSeatIds.Contains(s.ShowtimeSeatId));

        var ids = await query.Select(s => s.ShowtimeSeatId).ToListAsync(ct);
        if (ids.Count == 0) return 0;

        // Điều kiện HeldByBookingId vẫn được giữ khi UPDATE để không giải phóng nhầm
        // trong trường hợp ghế đã hết hạn và bị người khác giành lấy trước đó.
        return await db.ShowtimeSeats
            .Where(s => ids.Contains(s.ShowtimeSeatId)
                     && s.HeldByBookingId == bookingId
                     && s.Status == BookingStates.SeatHeld)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingStates.SeatAvailable)
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }

    /// <summary>Giải phóng toàn bộ ghế đang giữ của một đơn (dùng khi hủy hoặc hết hạn).</summary>
    public Task<int> ReleaseAllAsync(int bookingId, CancellationToken ct) =>
        ReleaseAsync(bookingId, [], ct);

    /// <summary>
    /// Chuyển các ghế đang giữ của đơn sang trạng thái đã bán.
    /// Chỉ chuyển những ghế còn thực sự do đơn giữ, nên ghế đã bị người khác giành lại sẽ không bị bán nhầm.
    /// </summary>
    public async Task<int> MarkSoldAsync(int bookingId, int showtimeId, CancellationToken ct)
    {
        // Khi chuyển sang Sold phải xóa cả HeldByBookingId và HoldExpiresAt
        // để không vi phạm CHECK constraint trạng thái ghế.
        return await db.ShowtimeSeats
            .Where(s => s.ShowtimeId == showtimeId
                     && s.HeldByBookingId == bookingId
                     && s.Status == BookingStates.SeatHeld)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, BookingStates.SeatSold)
                .SetProperty(s => s.HeldByBookingId, (int?)null)
                .SetProperty(s => s.HoldExpiresAt, (DateTime?)null), ct);
    }
}
