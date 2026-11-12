using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Data;

public class CinemaDbContext(DbContextOptions<CinemaDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<CinemaManager> CinemaManagers => Set<CinemaManager>();
    public DbSet<Staff> Staffs => Set<Staff>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<Auditorium> Auditoriums => Set<Auditorium>();
    public DbSet<SeatType> SeatTypes => Set<SeatType>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Screening> Screenings => Set<Screening>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ScreeningSeat> ScreeningSeats => Set<ScreeningSeat>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ConcessionItem> ConcessionItems => Set<ConcessionItem>();
    public DbSet<BookingConcession> BookingConcessions => Set<BookingConcession>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Roles
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleName).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.RoleName).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(255);

            entity.HasData(
                new Role { RoleId = 1, RoleName = "Admin", Description = "System Administrator" },
                new Role { RoleId = 2, RoleName = "CinemaManager", Description = "Cinema Operations Manager" },
                new Role { RoleId = 3, RoleName = "Staff", Description = "Box-office and Counter Staff" },
                new Role { RoleId = 4, RoleName = "Customer", Description = "Moviegoer Customer" }
            );
        });

        // 2. Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.ToTable(t => t.HasCheckConstraint("CK_Users_Status", "[Status] IN ('Active','Inactive','Locked')"));

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.RoleId).HasDatabaseName("IX_Users_Role");
        });

        // 3. Admins
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.AdminId);
            entity.Property(e => e.AdminCode).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.AdminCode).IsUnique();

            entity.HasOne(e => e.User)
                .WithOne(u => u.Admin)
                .HasForeignKey<Admin>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // 4. CinemaManagers
        modelBuilder.Entity<CinemaManager>(entity =>
        {
            entity.HasKey(e => e.ManagerId);
            entity.Property(e => e.ManagerCode).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.ManagerCode).IsUnique();

            entity.HasOne(e => e.User)
                .WithOne(u => u.CinemaManager)
                .HasForeignKey<CinemaManager>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // 5. Staffs
        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(e => e.StaffId);
            entity.Property(e => e.StaffCode).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.StaffCode).IsUnique();

            entity.HasOne(e => e.User)
                .WithOne(u => u.Staff)
                .HasForeignKey<Staff>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // 6. Customers
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.CustomerCode).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.CustomerCode).IsUnique();

            entity.HasOne(e => e.User)
                .WithOne(u => u.Customer)
                .HasForeignKey<Customer>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // 7. Movies
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.MovieId);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AgeRating).HasMaxLength(10).IsUnicode(false).HasDefaultValue("P");
            entity.Property(e => e.PosterUrl).HasMaxLength(1000);
            entity.Property(e => e.TrailerUrl).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Upcoming");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Movies_Duration", "[DurationMinutes] > 0");
                t.HasCheckConstraint("CK_Movies_Status", "[Status] IN ('Upcoming','Showing','Ended')");
            });
        });

        // 8. Genres
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.GenreId);
            entity.Property(e => e.GenreName).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.GenreName).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasData(
                new Genre { GenreId = 1, GenreName = "Hành động" },
                new Genre { GenreId = 2, GenreName = "Tình cảm" },
                new Genre { GenreId = 3, GenreName = "Hoạt hình" },
                new Genre { GenreId = 4, GenreName = "Kinh dị" },
                new Genre { GenreId = 5, GenreName = "Khoa học viễn tưởng" }
            );
        });

        // 9. MovieGenres
        modelBuilder.Entity<MovieGenre>(entity =>
        {
            entity.HasKey(e => new { e.MovieId, e.GenreId });

            entity.HasOne(e => e.Movie)
                .WithMany(m => m.MovieGenres)
                .HasForeignKey(e => e.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Genre)
                .WithMany(g => g.MovieGenres)
                .HasForeignKey(e => e.GenreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.GenreId).HasDatabaseName("IX_MovieGenres_Genre");
        });

        // 10. Auditoriums
        modelBuilder.Entity<Auditorium>(entity =>
        {
            entity.HasKey(e => e.AuditoriumId);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Active");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Auditoriums_Rows", "[TotalRows] > 0");
                t.HasCheckConstraint("CK_Auditoriums_Columns", "[TotalColumns] > 0");
                t.HasCheckConstraint("CK_Auditoriums_Status", "[Status] IN ('Active','Maintenance','Inactive')");
            });
        });

        // 11. SeatTypes
        modelBuilder.Entity<SeatType>(entity =>
        {
            entity.HasKey(e => e.SeatTypeId);
            entity.Property(e => e.TypeName).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.TypeName).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PriceMultiplier).HasPrecision(6, 3).HasDefaultValue(1.000m);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.ToTable(t => t.HasCheckConstraint("CK_SeatTypes_Multiplier", "[PriceMultiplier] > 0"));

            entity.HasData(
                new SeatType { SeatTypeId = 1, TypeName = "Standard", Description = "Ghế thường", PriceMultiplier = 1.000m, IsActive = true },
                new SeatType { SeatTypeId = 2, TypeName = "VIP", Description = "Ghế VIP", PriceMultiplier = 1.200m, IsActive = true },
                new SeatType { SeatTypeId = 3, TypeName = "Couple", Description = "Một đơn vị ghế đôi cho hai người, một vé", PriceMultiplier = 2.000m, IsActive = true }
            );
        });

        // 12. Seats
        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.SeatId);
            entity.Property(e => e.RowLabel).HasMaxLength(10).IsUnicode(false).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Active");

            entity.HasIndex(e => new { e.AuditoriumId, e.RowLabel, e.SeatNumber })
                .IsUnique()
                .HasDatabaseName("UQ_Seats_Position");

            entity.HasAlternateKey(e => new { e.SeatId, e.AuditoriumId })
                .HasName("UQ_Seats_Room");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Seats_Number", "[SeatNumber] > 0");
                t.HasCheckConstraint("CK_Seats_Status", "[Status] IN ('Active','Broken','Inactive')");
            });

            entity.HasOne(e => e.Auditorium)
                .WithMany(a => a.Seats)
                .HasForeignKey(e => e.AuditoriumId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SeatType)
                .WithMany(st => st.Seats)
                .HasForeignKey(e => e.SeatTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.SeatTypeId).HasDatabaseName("IX_Seats_Type");
        });

        // 13. Screenings
        modelBuilder.Entity<Screening>(entity =>
        {
            entity.HasKey(e => e.ScreeningId);
            entity.Property(e => e.StartAt).HasPrecision(0);
            entity.Property(e => e.EndAt).HasPrecision(0);
            entity.Property(e => e.CleanupMinutes).HasDefaultValue(15);
            entity.Property(e => e.BasePrice).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Scheduled");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasAlternateKey(e => new { e.ScreeningId, e.AuditoriumId })
                .HasName("UQ_Screenings_Room");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Screenings_Cleanup", "[CleanupMinutes] = 15");
                t.HasCheckConstraint("CK_Screenings_Price", "[BasePrice] >= 0");
                t.HasCheckConstraint("CK_Screenings_Status", "[Status] IN ('Scheduled','Ongoing','Finished','Cancelled')");
                t.HasCheckConstraint("CK_Screenings_TimeRange", "[EndAt] > [StartAt]");
            });

            entity.HasOne(e => e.Movie)
                .WithMany(m => m.Screenings)
                .HasForeignKey(e => e.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Auditorium)
                .WithMany(a => a.Screenings)
                .HasForeignKey(e => e.AuditoriumId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.MovieId, e.StartAt }).HasDatabaseName("IX_Screenings_MovieStart");
            entity.HasIndex(e => new { e.AuditoriumId, e.StartAt })
                .IncludeProperties(e => new { e.EndAt, e.Status })
                .HasDatabaseName("IX_Screenings_RoomStart");
        });

        // 14. Bookings
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId);
            entity.Property(e => e.BookingCode).HasMaxLength(40).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.BookingCode).IsUnique();
            entity.Property(e => e.BookingDate).HasPrecision(0).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.BookingChannel).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasAlternateKey(e => new { e.BookingId, e.ScreeningId })
                .HasName("UQ_Bookings_Show");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Bookings_Total", "[TotalAmount] >= 0");
                t.HasCheckConstraint("CK_Bookings_Channel", "[BookingChannel] IN ('Online','Counter')");
                t.HasCheckConstraint("CK_Bookings_Status", "[Status] IN ('Pending','Confirmed','Cancelled','Expired','Refunded')");
                t.HasCheckConstraint("CK_Bookings_Expiry", "[ExpiresAt] > [BookingDate]");
                t.HasCheckConstraint("CK_Bookings_ChannelActors",
                    "([BookingChannel] = 'Online' AND [CustomerId] IS NOT NULL AND [StaffId] IS NULL) OR ([BookingChannel] = 'Counter' AND [StaffId] IS NOT NULL)");
            });

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Bookings)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Staff)
                .WithMany(s => s.Bookings)
                .HasForeignKey(e => e.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Screening)
                .WithMany(s => s.Bookings)
                .HasForeignKey(e => e.ScreeningId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.CustomerId, e.BookingDate }).HasDatabaseName("IX_Bookings_CustomerDate");
            entity.HasIndex(e => e.StaffId).HasDatabaseName("IX_Bookings_Staff");
            entity.HasIndex(e => e.ScreeningId).HasDatabaseName("IX_Bookings_Show");
        });

        // 15. ScreeningSeats
        modelBuilder.Entity<ScreeningSeat>(entity =>
        {
            entity.HasKey(e => e.ScreeningSeatId);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Available");
            entity.Property(e => e.HoldExpiresAt).HasPrecision(0);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.ScreeningId, e.SeatId })
                .IsUnique()
                .HasDatabaseName("UQ_ScreeningSeats_Seat");

            entity.HasAlternateKey(e => new { e.ScreeningSeatId, e.ScreeningId })
                .HasName("UQ_ScreeningSeats_Show");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ScreeningSeats_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_ScreeningSeats_Status", "[Status] IN ('Available','Held','Sold','Blocked')");
                t.HasCheckConstraint("CK_ScreeningSeats_HeldState",
                    "([Status] = 'Held' AND [HeldByBookingId] IS NOT NULL AND [HoldExpiresAt] IS NOT NULL) OR ([Status] <> 'Held' AND [HeldByBookingId] IS NULL AND [HoldExpiresAt] IS NULL)");
            });

            entity.HasOne(e => e.Screening)
                .WithMany(s => s.ScreeningSeats)
                .HasPrincipalKey(s => new { s.ScreeningId, s.AuditoriumId })
                .HasForeignKey(e => new { e.ScreeningId, e.AuditoriumId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Seat)
                .WithMany(s => s.ScreeningSeats)
                .HasPrincipalKey(s => new { s.SeatId, s.AuditoriumId })
                .HasForeignKey(e => new { e.SeatId, e.AuditoriumId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.HeldByBooking)
                .WithMany(b => b.HeldScreeningSeats)
                .HasPrincipalKey(b => new { b.BookingId, b.ScreeningId })
                .HasForeignKey(e => new { e.HeldByBookingId, e.ScreeningId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.Status, e.HoldExpiresAt }).HasDatabaseName("IX_ScreeningSeats_Expiry");
            entity.HasIndex(e => e.HeldByBookingId).HasDatabaseName("IX_ScreeningSeats_Booking");
            entity.HasIndex(e => e.SeatId).HasDatabaseName("IX_ScreeningSeats_PhysicalSeat");
        });

        // 16. Tickets
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.TicketId);
            entity.Property(e => e.TicketCode).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.TicketCode).IsUnique();
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Valid");
            entity.Property(e => e.IssuedAt).HasPrecision(0).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.CheckedInAt).HasPrecision(0);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Tickets_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Tickets_Status", "[Status] IN ('Valid','CheckedIn','Cancelled')");
                t.HasCheckConstraint("CK_Tickets_CheckInState",
                    "([Status] = 'CheckedIn' AND [CheckedInAt] IS NOT NULL) OR ([Status] <> 'CheckedIn' AND [CheckedInAt] IS NULL)");
            });

            entity.HasIndex(e => e.ScreeningSeatId)
                .IsUnique()
                .HasFilter("[Status] <> 'Cancelled'")
                .HasDatabaseName("UX_Tickets_ActiveSeat");

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.Tickets)
                .HasPrincipalKey(b => new { b.BookingId, b.ScreeningId })
                .HasForeignKey(e => new { e.BookingId, e.ScreeningId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ScreeningSeat)
                .WithMany(ss => ss.Tickets)
                .HasPrincipalKey(ss => new { ss.ScreeningSeatId, ss.ScreeningId })
                .HasForeignKey(e => new { e.ScreeningSeatId, e.ScreeningId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.BookingId).HasDatabaseName("IX_Tickets_Booking");
        });

        // 17. ConcessionItems
        modelBuilder.Entity<ConcessionItem>(entity =>
        {
            entity.HasKey(e => e.ConcessionItemId);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.StockQuantity).HasDefaultValue(0);
            entity.Property(e => e.LowStockThreshold).HasDefaultValue(10);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Concessions_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Concessions_Stock", "[StockQuantity] >= 0");
                t.HasCheckConstraint("CK_Concessions_Threshold", "[LowStockThreshold] >= 0");
                t.HasCheckConstraint("CK_Concessions_Status", "[Status] IN ('Active','Inactive')");
            });

            entity.HasData(
                new ConcessionItem { ConcessionItemId = 1, Name = "Popcorn Caramel", Description = "Bắp caramel", Price = 65000m, StockQuantity = 100, LowStockThreshold = 10, Status = "Active" },
                new ConcessionItem { ConcessionItemId = 2, Name = "Coca-Cola 32oz", Description = "Nước ngọt", Price = 30000m, StockQuantity = 100, LowStockThreshold = 10, Status = "Active" },
                new ConcessionItem { ConcessionItemId = 3, Name = "Combo Solo", Description = "Combo quản lý tồn độc lập", Price = 85000m, StockQuantity = 50, LowStockThreshold = 10, Status = "Active" }
            );
        });

        // 18. BookingConcessions
        modelBuilder.Entity<BookingConcession>(entity =>
        {
            entity.HasKey(e => e.BookingConcessionId);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal)
                .HasPrecision(18, 2)
                .HasComputedColumnSql("CONVERT(decimal(18,2), [Quantity] * [UnitPrice])", stored: true);

            entity.HasIndex(e => new { e.BookingId, e.ConcessionItemId })
                .IsUnique()
                .HasDatabaseName("UQ_BookingConcessions_Item");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_BookingConcessions_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_BookingConcessions_UnitPrice", "[UnitPrice] >= 0");
            });

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.BookingConcessions)
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ConcessionItem)
                .WithMany(c => c.BookingConcessions)
                .HasForeignKey(e => e.ConcessionItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ConcessionItemId).HasDatabaseName("IX_BookingConcessions_Item");
        });

        // 19. Payments
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId);
            entity.Property(e => e.PaymentCode).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.HasIndex(e => e.PaymentCode).IsUnique();
            entity.Property(e => e.Provider).HasMaxLength(30).IsUnicode(false).IsRequired();
            entity.Property(e => e.TransactionRef).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.RefundAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.PaymentMethod).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.PaymentStatus).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.PaidAt).HasPrecision(0);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                t.HasCheckConstraint("CK_Payments_Method", "[PaymentMethod] IN ('Cash','VNPay_Simulated','MoMo_Simulated')");
                t.HasCheckConstraint("CK_Payments_Status", "[PaymentStatus] IN ('Pending','Success','Failed','Cancelled')");
                t.HasCheckConstraint("CK_Payments_RefundRange", "[RefundAmount] >= 0 AND [RefundAmount] <= [Amount]");
                t.HasCheckConstraint("CK_Payments_RefundSuccess", "[RefundAmount] = 0 OR [PaymentStatus] = 'Success'");
                t.HasCheckConstraint("CK_Payments_PaidState",
                    "([PaymentStatus] = 'Success' AND [PaidAt] IS NOT NULL) OR ([PaymentStatus] <> 'Success' AND [PaidAt] IS NULL)");
            });

            entity.HasIndex(e => e.BookingId)
                .IsUnique()
                .HasFilter("[PaymentStatus] = 'Success'")
                .HasDatabaseName("UX_Payments_Success");

            entity.HasIndex(e => new { e.Provider, e.TransactionRef })
                .IsUnique()
                .HasFilter("[TransactionRef] IS NOT NULL")
                .HasDatabaseName("UX_Payments_ProviderRef");

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.Payments)
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.BookingId).HasDatabaseName("IX_Payments_Booking");
        });
    }
}
