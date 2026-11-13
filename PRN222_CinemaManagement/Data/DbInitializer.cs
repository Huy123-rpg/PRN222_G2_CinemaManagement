using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Services;

namespace PRN222_CinemaManagement.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(CinemaDbContext context, IPasswordService passwordService)
    {
        // 1. Ensure Database created and applied
        await context.Database.EnsureCreatedAsync();

        string defaultPasswordHash = passwordService.HashPassword("123456");

        // 2. Seed Default Accounts if Users table is empty
        if (!await context.Users.AnyAsync())
        {
            // Admin
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@cinemadb.com",
                FullName = "System Administrator",
                PhoneNumber = "0900000001",
                RoleId = 1, // Admin
                Status = "Active",
                PasswordHash = defaultPasswordHash,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            context.Admins.Add(new Admin
            {
                UserId = adminUser.UserId,
                AdminCode = "ADM001"
            });

            // Cinema Manager
            var managerUser = new User
            {
                Username = "manager",
                Email = "manager@cinemadb.com",
                FullName = "Cinema Manager",
                PhoneNumber = "0900000002",
                RoleId = 2, // CinemaManager
                Status = "Active",
                PasswordHash = defaultPasswordHash,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(managerUser);
            await context.SaveChangesAsync();

            context.CinemaManagers.Add(new CinemaManager
            {
                UserId = managerUser.UserId,
                ManagerCode = "MGR001"
            });

            // Staff
            var staffUser = new User
            {
                Username = "staff",
                Email = "staff@cinemadb.com",
                FullName = "Counter Staff",
                PhoneNumber = "0900000003",
                RoleId = 3, // Staff
                Status = "Active",
                PasswordHash = defaultPasswordHash,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(staffUser);
            await context.SaveChangesAsync();

            context.Staffs.Add(new Staff
            {
                UserId = staffUser.UserId,
                StaffCode = "STF001"
            });

            // Customer
            var customerUser = new User
            {
                Username = "customer",
                Email = "customer@cinemadb.com",
                FullName = "Nguyen Van A",
                PhoneNumber = "0900000004",
                RoleId = 4, // Customer
                Status = "Active",
                PasswordHash = defaultPasswordHash,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(customerUser);
            await context.SaveChangesAsync();

            context.Customers.Add(new Customer
            {
                UserId = customerUser.UserId,
                CustomerCode = "CUS001"
            });

            await context.SaveChangesAsync();
        }
        else
        {
            // Cập nhật lại mật khẩu thành 123456 cho các tài khoản mặc định
            var defaultUsers = await context.Users
                .Where(u => u.Username == "admin" || u.Username == "manager" || u.Username == "staff" || u.Username == "customer")
                .ToListAsync();

            foreach (var user in defaultUsers)
            {
                if (!passwordService.VerifyPassword("123456", user.PasswordHash))
                {
                    user.PasswordHash = defaultPasswordHash;
                }
            }
            await context.SaveChangesAsync();
        }

        // 3. Seed Sample Auditoriums and Seats
        var standardTypeId = await context.SeatTypes.Where(st => st.TypeName == "Standard").Select(st => st.SeatTypeId).FirstOrDefaultAsync();
        var vipTypeId = await context.SeatTypes.Where(st => st.TypeName == "VIP").Select(st => st.SeatTypeId).FirstOrDefaultAsync();
        var coupleTypeId = await context.SeatTypes.Where(st => st.TypeName == "Couple").Select(st => st.SeatTypeId).FirstOrDefaultAsync();

        if (standardTypeId == 0) standardTypeId = 1;
        if (vipTypeId == 0) vipTypeId = 2;
        if (coupleTypeId == 0) coupleTypeId = 3;

        if (!await context.Auditoriums.AnyAsync())
        {
            var aud1 = new Auditorium { Name = "Phòng chiếu 1", TotalRows = 8, TotalColumns = 10, Status = "Active" };
            var aud2 = new Auditorium { Name = "Phòng chiếu 2 (VIP / IMAX)", TotalRows = 6, TotalColumns = 8, Status = "Active" };
            var aud3 = new Auditorium { Name = "Phòng chiếu 3 (Deluxe)", TotalRows = 5, TotalColumns = 6, Status = "Active" };

            context.Auditoriums.AddRange(aud1, aud2, aud3);
            await context.SaveChangesAsync();

            // Sinh ghế cho Phòng 1 (8 hàng x 10 cột)
            var seats1 = new List<Seat>();
            for (int r = 1; r <= 8; r++)
            {
                string rowLabel = ((char)('A' + r - 1)).ToString();
                int typeId = (r >= 7) ? vipTypeId : standardTypeId;
                for (int c = 1; c <= 10; c++)
                {
                    seats1.Add(new Seat { AuditoriumId = aud1.AuditoriumId, RowLabel = rowLabel, SeatNumber = c, SeatTypeId = typeId, Status = "Active" });
                }
            }
            context.Seats.AddRange(seats1);

            // Sinh ghế cho Phòng 2 (6 hàng x 8 cột, có ghế VIP và Couple)
            var seats2 = new List<Seat>();
            for (int r = 1; r <= 6; r++)
            {
                string rowLabel = ((char)('A' + r - 1)).ToString();
                int typeId = (r >= 5) ? coupleTypeId : ((r >= 3) ? vipTypeId : standardTypeId);
                for (int c = 1; c <= 8; c++)
                {
                    seats2.Add(new Seat { AuditoriumId = aud2.AuditoriumId, RowLabel = rowLabel, SeatNumber = c, SeatTypeId = typeId, Status = "Active" });
                }
            }
            context.Seats.AddRange(seats2);

            // Sinh ghế cho Phòng 3 (5 hàng x 6 cột)
            var seats3 = new List<Seat>();
            for (int r = 1; r <= 5; r++)
            {
                string rowLabel = ((char)('A' + r - 1)).ToString();
                int typeId = (r >= 4) ? vipTypeId : standardTypeId;
                for (int c = 1; c <= 6; c++)
                {
                    seats3.Add(new Seat { AuditoriumId = aud3.AuditoriumId, RowLabel = rowLabel, SeatNumber = c, SeatTypeId = typeId, Status = "Active" });
                }
            }
            context.Seats.AddRange(seats3);

            await context.SaveChangesAsync();
        }

        // 4. Seed Danh sách Phim phong phú (Movies)
        if (await context.Movies.CountAsync() < 4)
        {
            var existingTitles = await context.Movies.Select(m => m.Title).ToListAsync();
            var moviesToAdd = new List<Movie>();

            if (!existingTitles.Contains("Mai (2024)"))
            {
                moviesToAdd.Add(new Movie
                {
                    Title = "Mai (2024)",
                    Description = "Phim điện ảnh tâm lý tình cảm gia đình của Trấn Thành.",
                    DurationMinutes = 131,
                    ReleaseDate = new DateOnly(2024, 2, 10),
                    AgeRating = "T18",
                    Status = "Showing"
                });
            }

            if (!existingTitles.Contains("Đào, Phở và Piano"))
            {
                moviesToAdd.Add(new Movie
                {
                    Title = "Đào, Phở và Piano",
                    Description = "Khúc tráng ca hào hùng về Hà Nội mùa đông năm 1946.",
                    DurationMinutes = 100,
                    ReleaseDate = new DateOnly(2024, 2, 10),
                    AgeRating = "T13",
                    Status = "Showing"
                });
            }

            if (!existingTitles.Contains("Kung Fu Panda 4"))
            {
                moviesToAdd.Add(new Movie
                {
                    Title = "Kung Fu Panda 4",
                    Description = "Cuộc phiêu lưu hài hước mới của gấu Po và đại sư Shifu.",
                    DurationMinutes = 94,
                    ReleaseDate = new DateOnly(2024, 3, 8),
                    AgeRating = "P",
                    Status = "Showing"
                });
            }

            if (!existingTitles.Contains("Dune: Hành Tinh Cát 2"))
            {
                moviesToAdd.Add(new Movie
                {
                    Title = "Dune: Hành Tinh Cát 2",
                    Description = "Bom tấn viễn tưởng sử thi đỉnh cao của đạo diễn Denis Villeneuve.",
                    DurationMinutes = 166,
                    ReleaseDate = new DateOnly(2024, 3, 1),
                    AgeRating = "T16",
                    Status = "Showing"
                });
            }

            if (moviesToAdd.Any())
            {
                context.Movies.AddRange(moviesToAdd);
                await context.SaveChangesAsync();
            }
        }

        // 5. Seed Suất chiếu mẫu & Đơn vé mẫu để test demo (UC-10, 11, 12, 14, 18, TEC-06)
        if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-ONLINE-SUCCESS01") || await context.Bookings.CountAsync() < 3)
        {
            var customer = await context.Customers.Include(c => c.User).FirstOrDefaultAsync();
            var staff = await context.Staffs.Include(s => s.User).FirstOrDefaultAsync();
            var audList = await context.Auditoriums.ToListAsync();
            var movieList = await context.Movies.ToListAsync();

            if (customer != null && staff != null && audList.Any() && movieList.Any())
            {
                var movieMai = movieList.FirstOrDefault(m => m.Title.Contains("Mai")) ?? movieList[0];
                var movieDao = movieList.FirstOrDefault(m => m.Title.Contains("Đào")) ?? movieList[0];
                var moviePanda = movieList.FirstOrDefault(m => m.Title.Contains("Panda")) ?? movieList[0];
                var movieDune = movieList.FirstOrDefault(m => m.Title.Contains("Dune")) ?? movieList[0];

                var aud1 = audList[0];
                var aud2 = audList.Count > 1 ? audList[1] : aud1;

                // Suất chiếu 1: Ngày mai 19:00 (Phim Mai tại Phòng 1) - Có thể hủy (> 60 phút)
                var st1 = await context.Screenings.FirstOrDefaultAsync(s => s.MovieId == movieMai.MovieId && s.AuditoriumId == aud1.AuditoriumId);
                if (st1 == null)
                {
                    var startAt = DateTime.UtcNow.AddDays(1).Date.AddHours(19);
                    st1 = new Screening
                    {
                        MovieId = movieMai.MovieId,
                        AuditoriumId = aud1.AuditoriumId,
                        StartAt = startAt,
                        EndAt = startAt.AddMinutes(movieMai.DurationMinutes),
                        BasePrice = 80000m,
                        Status = "Scheduled"
                    };
                    context.Screenings.Add(st1);
                    await context.SaveChangesAsync();
                }

                // Suất chiếu 2: Hôm nay 20:00 (Phim Đào, Phở tại Phòng 2)
                var st2 = await context.Screenings.FirstOrDefaultAsync(s => s.MovieId == movieDao.MovieId && s.AuditoriumId == aud2.AuditoriumId);
                if (st2 == null)
                {
                    var startAt = DateTime.UtcNow.Date.AddHours(20);
                    st2 = new Screening
                    {
                        MovieId = movieDao.MovieId,
                        AuditoriumId = aud2.AuditoriumId,
                        StartAt = startAt,
                        EndAt = startAt.AddMinutes(movieDao.DurationMinutes),
                        BasePrice = 85000m,
                        Status = "Scheduled"
                    };
                    context.Screenings.Add(st2);
                    await context.SaveChangesAsync();
                }

                // Suất chiếu 3: Sát giờ chiếu (còn 30 phút nữa chiếu) - Dùng test quy tắc không cho hủy (< 60 phút)
                var st3 = await context.Screenings.FirstOrDefaultAsync(s => s.MovieId == moviePanda.MovieId && s.AuditoriumId == aud1.AuditoriumId);
                if (st3 == null)
                {
                    var startAt = DateTime.UtcNow.AddMinutes(30);
                    st3 = new Screening
                    {
                        MovieId = moviePanda.MovieId,
                        AuditoriumId = aud1.AuditoriumId,
                        StartAt = startAt, // Còn 30 phút nữa chiếu (< 60 phút)
                        EndAt = startAt.AddMinutes(moviePanda.DurationMinutes),
                        BasePrice = 75000m,
                        Status = "Scheduled"
                    };
                    context.Screenings.Add(st3);
                    await context.SaveChangesAsync();
                }

                // Đảm bảo ScreeningSeats cho các suất chiếu
                async Task EnsureScreeningSeats(Screening screening, int audId)
                {
                    if (!await context.ScreeningSeats.AnyAsync(ss => ss.ScreeningId == screening.ScreeningId))
                    {
                        var pSeats = await context.Seats.Where(s => s.AuditoriumId == audId).Take(12).ToListAsync();
                        var sSeats = pSeats.Select(ps => new ScreeningSeat
                        {
                            ScreeningId = screening.ScreeningId,
                            AuditoriumId = audId,
                            SeatId = ps.SeatId,
                            Price = screening.BasePrice,
                            Status = "Available"
                        }).ToList();
                        context.ScreeningSeats.AddRange(sSeats);
                        await context.SaveChangesAsync();
                    }
                }

                await EnsureScreeningSeats(st1, aud1.AuditoriumId);
                await EnsureScreeningSeats(st2, aud2.AuditoriumId);
                await EnsureScreeningSeats(st3, aud1.AuditoriumId);

                // --- Đơn 1: Khách Customer mua vé xem "Mai" ngày mai (ĐỦ điều kiện hủy, xem QR) ---
                if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-20261103-DEMO01"))
                {
                    var seatsForSt1 = await context.ScreeningSeats.Where(ss => ss.ScreeningId == st1.ScreeningId && ss.Status == "Available").Take(2).ToListAsync();
                    if (seatsForSt1.Count >= 2)
                    {
                        var b1 = new Booking
                        {
                            BookingCode = "BK-20261103-DEMO01",
                            CustomerId = customer.CustomerId,
                            ScreeningId = st1.ScreeningId,
                            BookingDate = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                            TotalAmount = 160000m,
                            BookingChannel = "Online",
                            Status = "Confirmed"
                        };
                        context.Bookings.Add(b1);
                        await context.SaveChangesAsync();

                        seatsForSt1[0].Status = "Sold";
                        seatsForSt1[1].Status = "Sold";

                        context.Tickets.Add(new Ticket { BookingId = b1.BookingId, ScreeningId = st1.ScreeningId, ScreeningSeatId = seatsForSt1[0].ScreeningSeatId, TicketCode = "TK-MAI-A1-8899", Price = 80000m, Status = "Valid", IssuedAt = DateTime.UtcNow });
                        context.Tickets.Add(new Ticket { BookingId = b1.BookingId, ScreeningId = st1.ScreeningId, ScreeningSeatId = seatsForSt1[1].ScreeningSeatId, TicketCode = "TK-MAI-A2-8899", Price = 80000m, Status = "Valid", IssuedAt = DateTime.UtcNow });
                        await context.SaveChangesAsync();
                    }
                }

                // --- Đơn 2: Khách Customer mua vé "Kung Fu Panda 4" sát giờ chiếu (TEST KHÔNG ĐƯỢC HỦY vì < 60p) ---
                if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-PANDA-DEMO02"))
                {
                    var seatsForSt3 = await context.ScreeningSeats.Where(ss => ss.ScreeningId == st3.ScreeningId && ss.Status == "Available").Take(1).ToListAsync();
                    if (seatsForSt3.Any())
                    {
                        var b2 = new Booking
                        {
                            BookingCode = "BK-PANDA-DEMO02",
                            CustomerId = customer.CustomerId,
                            ScreeningId = st3.ScreeningId,
                            BookingDate = DateTime.UtcNow.AddHours(-1),
                            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                            TotalAmount = 75000m,
                            BookingChannel = "Online",
                            Status = "Confirmed"
                        };
                        context.Bookings.Add(b2);
                        await context.SaveChangesAsync();

                        seatsForSt3[0].Status = "Sold";

                        context.Tickets.Add(new Ticket { BookingId = b2.BookingId, ScreeningId = st3.ScreeningId, ScreeningSeatId = seatsForSt3[0].ScreeningSeatId, TicketCode = "TK-PANDA-B5-1234", Price = 75000m, Status = "Valid", IssuedAt = DateTime.UtcNow.AddHours(-1) });
                        await context.SaveChangesAsync();
                    }
                }

                // --- Đơn 3: Khách mua tại quầy (Counter) xem "Đào, Phở và Piano" (1 vé CheckedIn, 1 vé Valid để TEST SOÁT VÉ) ---
                if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-COUNTER-DEMO03"))
                {
                    var seatsForSt2 = await context.ScreeningSeats.Where(ss => ss.ScreeningId == st2.ScreeningId && ss.Status == "Available").Take(2).ToListAsync();
                    if (seatsForSt2.Count >= 2)
                    {
                        var b3 = new Booking
                        {
                            BookingCode = "BK-COUNTER-DEMO03",
                            StaffId = staff.StaffId,
                            CustomerId = null, // Counter booking: Staff bán cho khách vãng lai
                            ScreeningId = st2.ScreeningId,
                            BookingDate = DateTime.UtcNow.AddHours(-2),
                            ExpiresAt = DateTime.UtcNow.AddHours(1),
                            TotalAmount = 170000m,
                            BookingChannel = "Counter",
                            Status = "Confirmed"
                        };
                        context.Bookings.Add(b3);
                        await context.SaveChangesAsync();

                        seatsForSt2[0].Status = "Sold";
                        seatsForSt2[1].Status = "Sold";

                        // Vé 1: ĐÃ SOÁT VÉ (CheckedIn)
                        context.Tickets.Add(new Ticket
                        {
                            BookingId = b3.BookingId,
                            ScreeningId = st2.ScreeningId,
                            ScreeningSeatId = seatsForSt2[0].ScreeningSeatId,
                            TicketCode = "TK-DAO-C3-5566",
                            Price = 85000m,
                            Status = "CheckedIn",
                            IssuedAt = DateTime.UtcNow.AddHours(-2),
                            CheckedInAt = DateTime.UtcNow.AddMinutes(-10) // Đã vào cách đây 10p
                        });

                        // Vé 2: HỢP LỆ CHƯA VÀO (Valid) - Để nhân viên test bấm soát vé thành công
                        context.Tickets.Add(new Ticket
                        {
                            BookingId = b3.BookingId,
                            ScreeningId = st2.ScreeningId,
                            ScreeningSeatId = seatsForSt2[1].ScreeningSeatId,
                            TicketCode = "TK-DAO-C4-5567",
                            Price = 85000m,
                            Status = "Valid",
                            IssuedAt = DateTime.UtcNow.AddHours(-2)
                        });

                        await context.SaveChangesAsync();
                    }
                }

                // --- Đơn 4: Đơn vé ĐÃ BỊ HỦY (Cancelled) để TEST QUÉT VÉ HỦY BỊ BÁO LỖI ---
                if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-CANCELLED-DEMO04"))
                {
                    var seatsForSt1_extra = await context.ScreeningSeats.Where(ss => ss.ScreeningId == st1.ScreeningId && ss.Status == "Available").Take(1).ToListAsync();
                    if (seatsForSt1_extra.Any())
                    {
                        var b4 = new Booking
                        {
                            BookingCode = "BK-CANCELLED-DEMO04",
                            CustomerId = customer.CustomerId,
                            ScreeningId = st1.ScreeningId,
                            BookingDate = DateTime.UtcNow.AddDays(-1),
                            ExpiresAt = DateTime.UtcNow.AddDays(1),
                            TotalAmount = 80000m,
                            BookingChannel = "Online",
                            Status = "Cancelled"
                        };
                        context.Bookings.Add(b4);
                        await context.SaveChangesAsync();

                        context.Tickets.Add(new Ticket
                        {
                            BookingId = b4.BookingId,
                            ScreeningId = st1.ScreeningId,
                            ScreeningSeatId = seatsForSt1_extra[0].ScreeningSeatId,
                            TicketCode = "TK-HUY-D1-9999",
                            Price = 80000m,
                            Status = "Cancelled",
                            IssuedAt = DateTime.UtcNow.AddDays(-1)
                        });

                        await context.SaveChangesAsync();
                    }
                }

                // --- Đơn 5: Đơn vé MỚI xem phim "Dune: Hành tinh cát" ngày mai (ĐỦ 100% ĐIỀU KIỆN ĐỂ TEST BẤM NÚT "HỦY VÉ") ---
                if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-ONLINE-SUCCESS01"))
                {
                    // Tạo hoặc cập nhật một suất chiếu ngày mai cho Dune tại phòng 2
                    var stDune = await context.Screenings.FirstOrDefaultAsync(s => s.MovieId == movieDune.MovieId);
                    if (stDune == null)
                    {
                        var startAt = DateTime.UtcNow.AddDays(1).Date.AddHours(20);
                        stDune = new Screening
                        {
                            MovieId = movieDune.MovieId,
                            AuditoriumId = aud2.AuditoriumId,
                            StartAt = startAt,
                            EndAt = startAt.AddMinutes(movieDune.DurationMinutes),
                            BasePrice = 90000m,
                            Status = "Scheduled"
                        };
                        context.Screenings.Add(stDune);
                        await context.SaveChangesAsync();
                    }

                    await EnsureScreeningSeats(stDune, stDune.AuditoriumId);

                    var seatForDune = await context.ScreeningSeats.Where(ss => ss.ScreeningId == stDune.ScreeningId && ss.Status == "Available").Take(1).ToListAsync();
                    if (seatForDune.Any())
                    {
                        var b5 = new Booking
                        {
                            BookingCode = "BK-ONLINE-SUCCESS01",
                            CustomerId = customer.CustomerId,
                            ScreeningId = stDune.ScreeningId,
                            BookingDate = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddDays(1),
                            TotalAmount = 90000m,
                            BookingChannel = "Online",
                            Status = "Confirmed"
                        };
                        context.Bookings.Add(b5);
                        await context.SaveChangesAsync();

                        seatForDune[0].Status = "Sold";

                        context.Tickets.Add(new Ticket
                        {
                            BookingId = b5.BookingId,
                            ScreeningId = stDune.ScreeningId,
                            ScreeningSeatId = seatForDune[0].ScreeningSeatId,
                            TicketCode = "TK-DUNE-B2-7788",
                            Price = 90000m,
                            Status = "Valid",
                            IssuedAt = DateTime.UtcNow
                        });

                        await context.SaveChangesAsync();
                    }
                }
            }
        }
    }
}
