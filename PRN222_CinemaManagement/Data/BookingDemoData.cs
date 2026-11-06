using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Data;

/// <summary>
/// Dữ liệu mẫu cho module đặt vé online: phim, suất chiếu và lưới ghế cho mỗi suất.
/// Chỉ sinh một lần khi chưa có dữ liệu; không ghi đè dữ liệu thật của hệ thống.
/// </summary>
public static class BookingDemoData
{
    /// <summary>
    /// Kiểm tra và bổ sung dữ liệu demo:
    ///  - 3 phim đang chiếu (Showing) với lịch chiếu trong 7 ngày tới;
    ///  - lưới ShowtimeSeat cho từng suất (giá = giá cơ bản × hệ số loại ghế);
    ///  - 2 tài khoản khách hàng phụ để demo tranh ghế giữa nhiều người.
    /// </summary>
    public static async Task SeedAsync(CinemaDbContext context)
    {
        var now = DateTime.UtcNow;

        // 1. Phim: chỉ tạo nếu chưa có phim nào (tránh đụng dữ liệu thật).
        if (!await context.Movies.AnyAsync())
        {
            context.Movies.AddRange(
                new Movie
                {
                    Title = "Hành trình ngôi sao",
                    Description = "Đoạn phim phiêu lưu không gian phục vụ demo module đặt vé.\nĐội thám hiểm cuối cùng của nhân loại tìm cách hồi sinh Trái Đất.",
                    DurationMinutes = 128, AgeRating = "P13",
                    ReleaseDate = new DateOnly(now.Year, now.Month, now.Day),
                    Status = "Showing",
                    PosterUrl = null
                },
                new Movie
                {
                    Title = "Tâm điểm mùa hè",
                    Description = "Phim tình cảm đô thị.\nHai người trẻ gặp lại nhau sau mười năm xa cách.",
                    DurationMinutes = 105, AgeRating = "P",
                    ReleaseDate = new DateOnly(now.Year, now.Month, now.Day),
                    Status = "Showing",
                    PosterUrl = null
                },
                new Movie
                {
                    Title = "Cỗ máy thời gian",
                    Description = "Phim hoạt hình cho cả nhà.\nCô bé lạc vào cỗ máy thời gian của ông ngoại.",
                    DurationMinutes = 96, AgeRating = "P6",
                    ReleaseDate = new DateOnly(now.Year, now.Month, now.Day),
                    Status = "Showing",
                    PosterUrl = null
                });
            await context.SaveChangesAsync();
        }

        // 2. Suất chiếu: tạo cho 3 ngày tới (mỗi ngày 2 suất) nếu suất chưa tồn tại.
        var showtimesCreated = false;
        var movies = await context.Movies.ToListAsync();
        if (movies.Count > 0 && !await context.Showtimes.AnyAsync())
        {
            var room = await context.Auditoriums.AsNoTracking().FirstOrDefaultAsync();
            if (room is not null)
            {
                for (var day = 0; day < 3; day++)
                {
                    var start = now.Date.AddDays(day + 1).AddHours(15).AddMinutes(30);
                    var start2 = now.Date.AddDays(day + 1).AddHours(19).AddMinutes(0);

                    for (var i = 0; i < movies.Count; i++)
                    {
                        var pick = (i + day) % movies.Count;
                        context.Showtimes.Add(new Showtime
                        {
                            MovieId = movies[pick].MovieId,
                            AuditoriumId = room.AuditoriumId,
                            StartAt = day % 2 == 0 ? start : start2,
                            EndAt = day % 2 == 0
                                ? start.AddMinutes(movies[pick].DurationMinutes)
                                : start2.AddMinutes(movies[pick].DurationMinutes),
                            BasePrice = 80_000m + (i * 10_000m),
                            Status = "Scheduled"
                        });
                    }
                }

                showtimesCreated = await context.SaveChangesAsync() > 0;
            }
        }

        if (!showtimesCreated) return;

        // 3. Lưới ghế cho tất cả suất: giá từng ghế = giá cơ bản của suất × hệ số loại ghế.
        var existingSeats = await context.ShowtimeSeats.Select(s => s.ShowtimeId).Distinct().ToListAsync();
        var missingShowtimes = await context.Showtimes.AsNoTracking()
            .Where(s => !existingSeats.Contains(s.ShowtimeId))
            .ToListAsync();

        if (missingShowtimes.Count == 0) return;

        var physicalSeats = await context.Seats.AsNoTracking()
            .Where(s => s.AuditoriumId == missingShowtimes[0].AuditoriumId)
            .ToListAsync();

        var multipliers = await context.SeatTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.SeatTypeId, t => t.PriceMultiplier);

        var rows = new List<ShowtimeSeat>();
        foreach (var show in missingShowtimes)
        {
            // Chỉ tạo ghế cho đúng phòng của suất chiếu (khóa chính suất chiếu bao gồm phòng).
            var roomSeats = physicalSeats.Where(s => s.AuditoriumId == show.AuditoriumId).ToList();
            foreach (var seat in roomSeats)
            {
                var multiplier = multipliers.TryGetValue(seat.SeatTypeId, out var m) ? m : 1.000m;
                rows.Add(new ShowtimeSeat
                {
                    ShowtimeId = show.ShowtimeId,
                    AuditoriumId = show.AuditoriumId,
                    SeatId = seat.SeatId,
                    Price = Math.Round(show.BasePrice * multiplier, 2),
                    Status = "Available"
                });
            }
        }

        context.ShowtimeSeats.AddRange(rows);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Tạo thêm 2 khách hàng demo (mật khẩu 123456) để kiểm tra luồng
    /// tranh ghế giữa hai người dùng trên cùng một trình duyệt.
    /// </summary>
    public static async Task SeedDemoCustomersAsync(CinemaDbContext context, Services.IPasswordService passwords)
    {
        var hash = passwords.HashPassword("123456");

        foreach (var (username, email, name) in new[]
        {
            ("customer2", "customer2@cinemadb.com", "Tran Thi B"),
            ("customer3", "customer3@cinemadb.com", "Le Van C")
        })
        {
            if (await context.Users.AnyAsync(u => u.Username == username)) continue;

            var user = new User
            {
                Username = username,
                Email = email,
                FullName = name,
                PhoneNumber = null,
                RoleId = 4, // Customer
                Status = "Active",
                PasswordHash = hash,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.Customers.Add(new Customer
            {
                UserId = user.UserId,
                CustomerCode = $"CUS{user.UserId:D4}"
            });
        }

        await context.SaveChangesAsync();
    }
}
