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

        // 3. Seed Sample Auditorium and Seats if not present
        if (!await context.Auditoriums.AnyAsync())
        {
            var auditorium = new Auditorium
            {
                Name = "Phòng chiếu 1",
                TotalRows = 8,
                TotalColumns = 10,
                Status = "Active"
            };
            context.Auditoriums.Add(auditorium);
            await context.SaveChangesAsync();

            var standardTypeId = await context.SeatTypes.Where(st => st.TypeName == "Standard").Select(st => st.SeatTypeId).FirstOrDefaultAsync();
            var vipTypeId = await context.SeatTypes.Where(st => st.TypeName == "VIP").Select(st => st.SeatTypeId).FirstOrDefaultAsync();

            var seats = new List<Seat>();
            for (int r = 1; r <= 8; r++)
            {
                string rowLabel = ((char)('A' + r - 1)).ToString();
                int seatTypeId = (r >= 7) ? vipTypeId : standardTypeId;

                for (int c = 1; c <= 10; c++)
                {
                    seats.Add(new Seat
                    {
                        AuditoriumId = auditorium.AuditoriumId,
                        RowLabel = rowLabel,
                        SeatNumber = c,
                        SeatTypeId = seatTypeId,
                        Status = "Active"
                    });
                }
            }

            context.Seats.AddRange(seats);
            await context.SaveChangesAsync();
        }
    }
}
