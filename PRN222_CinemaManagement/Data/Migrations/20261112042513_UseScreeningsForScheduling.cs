using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PRN222_CinemaManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseScreeningsForScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Screenings_CinemaRooms_CinemaRoomId",
                table: "Screenings");

            migrationBuilder.DropTable(name: "CinemaRooms");

            migrationBuilder.DropIndex(
                name: "IX_Screenings_CinemaRoomId_StartsAt",
                table: "Screenings");

            migrationBuilder.DropIndex(
                name: "IX_Screenings_MovieId_StartsAt",
                table: "Screenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Screenings_TimeRange",
                table: "Screenings");

            migrationBuilder.DropIndex(
                name: "IX_Movies_Title",
                table: "Movies");

            migrationBuilder.RenameColumn(
                name: "StartsAt",
                table: "Screenings",
                newName: "StartAt");

            migrationBuilder.RenameColumn(
                name: "EndsAt",
                table: "Screenings",
                newName: "EndAt");

            migrationBuilder.RenameColumn(
                name: "CinemaRoomId",
                table: "Screenings",
                newName: "AuditoriumId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Screenings",
                newName: "ScreeningId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Movies",
                newName: "MovieId");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "Screenings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CleanupMinutes",
                table: "Screenings",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Screenings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Screenings",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Scheduled");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Movies",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Upcoming",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.Sql("UPDATE [Movies] SET [Status] = CASE [Status] WHEN '0' THEN 'Upcoming' WHEN '1' THEN 'Showing' WHEN '2' THEN 'Ended' ELSE 'Upcoming' END;");

            migrationBuilder.Sql("UPDATE s SET [EndAt] = DATEADD(MINUTE, m.[DurationMinutes], s.[StartAt]) FROM [Screenings] s INNER JOIN [Movies] m ON m.[MovieId] = s.[MovieId];");

            migrationBuilder.AlterColumn<string>(
                name: "PosterUrl",
                table: "Movies",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgeRating",
                table: "Movies",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "P");

            migrationBuilder.AddColumn<string>(
                name: "TrailerUrl",
                table: "Movies",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Screenings_Room",
                table: "Screenings",
                columns: new[] { "ScreeningId", "AuditoriumId" });

            migrationBuilder.CreateTable(
                name: "Auditoriums",
                columns: table => new
                {
                    AuditoriumId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    TotalColumns = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoriums", x => x.AuditoriumId);
                    table.CheckConstraint("CK_Auditoriums_Columns", "[TotalColumns] > 0");
                    table.CheckConstraint("CK_Auditoriums_Rows", "[TotalRows] > 0");
                    table.CheckConstraint("CK_Auditoriums_Status", "[Status] IN ('Active','Maintenance','Inactive')");
                });


            migrationBuilder.CreateTable(
                name: "ConcessionItems",
                columns: table => new
                {
                    ConcessionItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionItems", x => x.ConcessionItemId);
                    table.CheckConstraint("CK_Concessions_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_Concessions_Status", "[Status] IN ('Active','Inactive')");
                    table.CheckConstraint("CK_Concessions_Stock", "[StockQuantity] >= 0");
                    table.CheckConstraint("CK_Concessions_Threshold", "[LowStockThreshold] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    GenreId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GenreName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.GenreId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "SeatTypes",
                columns: table => new
                {
                    SeatTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceMultiplier = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false, defaultValue: 1.000m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatTypes", x => x.SeatTypeId);
                    table.CheckConstraint("CK_SeatTypes_Multiplier", "[PriceMultiplier] > 0");
                });

            migrationBuilder.CreateTable(
                name: "MovieGenres",
                columns: table => new
                {
                    MovieId = table.Column<int>(type: "int", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieGenres", x => new { x.MovieId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_MovieGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "GenreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovieGenres_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "MovieId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.CheckConstraint("CK_Users_Status", "[Status] IN ('Active','Inactive','Locked')");
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    SeatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriumId = table.Column<int>(type: "int", nullable: false),
                    SeatTypeId = table.Column<int>(type: "int", nullable: false),
                    RowLabel = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.SeatId);
                    table.UniqueConstraint("UQ_Seats_Room", x => new { x.SeatId, x.AuditoriumId });
                    table.CheckConstraint("CK_Seats_Number", "[SeatNumber] > 0");
                    table.CheckConstraint("CK_Seats_Status", "[Status] IN ('Active','Broken','Inactive')");
                    table.ForeignKey(
                        name: "FK_Seats_Auditoriums_AuditoriumId",
                        column: x => x.AuditoriumId,
                        principalTable: "Auditoriums",
                        principalColumn: "AuditoriumId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seats_SeatTypes_SeatTypeId",
                        column: x => x.SeatTypeId,
                        principalTable: "SeatTypes",
                        principalColumn: "SeatTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    AdminId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.AdminId);
                    table.ForeignKey(
                        name: "FK_Admins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CinemaManagers",
                columns: table => new
                {
                    ManagerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManagerCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CinemaManagers", x => x.ManagerId);
                    table.ForeignKey(
                        name: "FK_CinemaManagers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_Customers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Staffs",
                columns: table => new
                {
                    StaffId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staffs", x => x.StaffId);
                    table.ForeignKey(
                        name: "FK_Staffs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    ScreeningId = table.Column<int>(type: "int", nullable: false),
                    BookingDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    BookingChannel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Pending"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingId);
                    table.UniqueConstraint("UQ_Bookings_Show", x => new { x.BookingId, x.ScreeningId });
                    table.CheckConstraint("CK_Bookings_Channel", "[BookingChannel] IN ('Online','Counter')");
                    table.CheckConstraint("CK_Bookings_ChannelActors", "([BookingChannel] = 'Online' AND [CustomerId] IS NOT NULL AND [StaffId] IS NULL) OR ([BookingChannel] = 'Counter' AND [StaffId] IS NOT NULL)");
                    table.CheckConstraint("CK_Bookings_Expiry", "[ExpiresAt] > [BookingDate]");
                    table.CheckConstraint("CK_Bookings_Status", "[Status] IN ('Pending','Confirmed','Cancelled','Expired','Refunded')");
                    table.CheckConstraint("CK_Bookings_Total", "[TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_Bookings_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Screenings_ScreeningId",
                        column: x => x.ScreeningId,
                        principalTable: "Screenings",
                        principalColumn: "ScreeningId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingConcessions",
                columns: table => new
                {
                    BookingConcessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    ConcessionItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "CONVERT(decimal(18,2), [Quantity] * [UnitPrice])", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingConcessions", x => x.BookingConcessionId);
                    table.CheckConstraint("CK_BookingConcessions_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_BookingConcessions_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_BookingConcessions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingConcessions_ConcessionItems_ConcessionItemId",
                        column: x => x.ConcessionItemId,
                        principalTable: "ConcessionItems",
                        principalColumn: "ConcessionItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    PaymentCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TransactionRef = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    PaymentMethod = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    PaymentStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    PaidAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Payments_Method", "[PaymentMethod] IN ('Cash','VNPay_Simulated','MoMo_Simulated')");
                    table.CheckConstraint("CK_Payments_PaidState", "([PaymentStatus] = 'Success' AND [PaidAt] IS NOT NULL) OR ([PaymentStatus] <> 'Success' AND [PaidAt] IS NULL)");
                    table.CheckConstraint("CK_Payments_RefundRange", "[RefundAmount] >= 0 AND [RefundAmount] <= [Amount]");
                    table.CheckConstraint("CK_Payments_RefundSuccess", "[RefundAmount] = 0 OR [PaymentStatus] = 'Success'");
                    table.CheckConstraint("CK_Payments_Status", "[PaymentStatus] IN ('Pending','Success','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Payments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningSeats",
                columns: table => new
                {
                    ScreeningSeatId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScreeningId = table.Column<int>(type: "int", nullable: false),
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    AuditoriumId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Available"),
                    HeldByBookingId = table.Column<int>(type: "int", nullable: true),
                    HoldExpiresAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningSeats", x => x.ScreeningSeatId);
                    table.UniqueConstraint("UQ_ScreeningSeats_Show", x => new { x.ScreeningSeatId, x.ScreeningId });
                    table.CheckConstraint("CK_ScreeningSeats_HeldState", "([Status] = 'Held' AND [HeldByBookingId] IS NOT NULL AND [HoldExpiresAt] IS NOT NULL) OR ([Status] <> 'Held' AND [HeldByBookingId] IS NULL AND [HoldExpiresAt] IS NULL)");
                    table.CheckConstraint("CK_ScreeningSeats_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_ScreeningSeats_Status", "[Status] IN ('Available','Held','Sold','Blocked')");
                    table.ForeignKey(
                        name: "FK_ScreeningSeats_Auditoriums_AuditoriumId",
                        column: x => x.AuditoriumId,
                        principalTable: "Auditoriums",
                        principalColumn: "AuditoriumId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScreeningSeats_Bookings_HeldByBookingId_ScreeningId",
                        columns: x => new { x.HeldByBookingId, x.ScreeningId },
                        principalTable: "Bookings",
                        principalColumns: new[] { "BookingId", "ScreeningId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScreeningSeats_Screenings_ScreeningId_AuditoriumId",
                        columns: x => new { x.ScreeningId, x.AuditoriumId },
                        principalTable: "Screenings",
                        principalColumns: new[] { "ScreeningId", "AuditoriumId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScreeningSeats_Seats_SeatId_AuditoriumId",
                        columns: x => new { x.SeatId, x.AuditoriumId },
                        principalTable: "Seats",
                        principalColumns: new[] { "SeatId", "AuditoriumId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    TicketId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    ScreeningSeatId = table.Column<int>(type: "int", nullable: false),
                    ScreeningId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Valid"),
                    IssuedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.TicketId);
                    table.CheckConstraint("CK_Tickets_CheckInState", "([Status] = 'CheckedIn' AND [CheckedInAt] IS NOT NULL) OR ([Status] <> 'CheckedIn' AND [CheckedInAt] IS NULL)");
                    table.CheckConstraint("CK_Tickets_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_Tickets_Status", "[Status] IN ('Valid','CheckedIn','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Tickets_Bookings_BookingId_ScreeningId",
                        columns: x => new { x.BookingId, x.ScreeningId },
                        principalTable: "Bookings",
                        principalColumns: new[] { "BookingId", "ScreeningId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ScreeningSeats_ScreeningSeatId_ScreeningId",
                        columns: x => new { x.ScreeningSeatId, x.ScreeningId },
                        principalTable: "ScreeningSeats",
                        principalColumns: new[] { "ScreeningSeatId", "ScreeningId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Screenings_ScreeningId",
                        column: x => x.ScreeningId,
                        principalTable: "Screenings",
                        principalColumn: "ScreeningId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ConcessionItems",
                columns: new[] { "ConcessionItemId", "Description", "LowStockThreshold", "Name", "Price", "Status", "StockQuantity" },
                values: new object[,]
                {
                    { 1, "Bắp caramel", 10, "Popcorn Caramel", 65000m, "Active", 100 },
                    { 2, "Nước ngọt", 10, "Coca-Cola 32oz", 30000m, "Active", 100 },
                    { 3, "Combo quản lý tồn độc lập", 10, "Combo Solo", 85000m, "Active", 50 }
                });

            migrationBuilder.InsertData(
                table: "Genres",
                columns: new[] { "GenreId", "Description", "GenreName" },
                values: new object[,]
                {
                    { 1, null, "Hành động" },
                    { 2, null, "Tình cảm" },
                    { 3, null, "Hoạt hình" },
                    { 4, null, "Kinh dị" },
                    { 5, null, "Khoa học viễn tưởng" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "RoleId", "Description", "RoleName" },
                values: new object[,]
                {
                    { 1, "System Administrator", "Admin" },
                    { 2, "Cinema Operations Manager", "CinemaManager" },
                    { 3, "Box-office and Counter Staff", "Staff" },
                    { 4, "Moviegoer Customer", "Customer" }
                });

            migrationBuilder.InsertData(
                table: "SeatTypes",
                columns: new[] { "SeatTypeId", "Description", "IsActive", "PriceMultiplier", "TypeName" },
                values: new object[,]
                {
                    { 1, "Ghế thường", true, 1.000m, "Standard" },
                    { 2, "Ghế VIP", true, 1.200m, "VIP" },
                    { 3, "Một đơn vị ghế đôi cho hai người, một vé", true, 2.000m, "Couple" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_MovieStart",
                table: "Screenings",
                columns: new[] { "MovieId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_RoomStart",
                table: "Screenings",
                columns: new[] { "AuditoriumId", "StartAt" })
                .Annotation("SqlServer:Include", new[] { "EndAt", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Screenings_Cleanup",
                table: "Screenings",
                sql: "[CleanupMinutes] = 15");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Screenings_Price",
                table: "Screenings",
                sql: "[BasePrice] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Screenings_Status",
                table: "Screenings",
                sql: "[Status] IN ('Scheduled','Ongoing','Finished','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Screenings_TimeRange",
                table: "Screenings",
                sql: "[EndAt] > [StartAt]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Movies_Duration",
                table: "Movies",
                sql: "[DurationMinutes] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Movies_Status",
                table: "Movies",
                sql: "[Status] IN ('Upcoming','Showing','Ended')");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_AdminCode",
                table: "Admins",
                column: "AdminCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Admins_UserId",
                table: "Admins",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auditoriums_Name",
                table: "Auditoriums",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingConcessions_Item",
                table: "BookingConcessions",
                column: "ConcessionItemId");

            migrationBuilder.CreateIndex(
                name: "UQ_BookingConcessions_Item",
                table: "BookingConcessions",
                columns: new[] { "BookingId", "ConcessionItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingCode",
                table: "Bookings",
                column: "BookingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerDate",
                table: "Bookings",
                columns: new[] { "CustomerId", "BookingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Show",
                table: "Bookings",
                column: "ScreeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Staff",
                table: "Bookings",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CinemaManagers_ManagerCode",
                table: "CinemaManagers",
                column: "ManagerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CinemaManagers_UserId",
                table: "CinemaManagers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionItems_Name",
                table: "ConcessionItems",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerCode",
                table: "Customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId",
                table: "Customers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_GenreName",
                table: "Genres",
                column: "GenreName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovieGenres_Genre",
                table: "MovieGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Booking",
                table: "Payments",
                column: "BookingId",
                unique: true,
                filter: "[PaymentStatus] = 'Success'");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentCode",
                table: "Payments",
                column: "PaymentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Payments_ProviderRef",
                table: "Payments",
                columns: new[] { "Provider", "TransactionRef" },
                unique: true,
                filter: "[TransactionRef] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_RoleName",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningSeats_AuditoriumId",
                table: "ScreeningSeats",
                column: "AuditoriumId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningSeats_Booking",
                table: "ScreeningSeats",
                column: "HeldByBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningSeats_Expiry",
                table: "ScreeningSeats",
                columns: new[] { "Status", "HoldExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningSeats_HeldByBookingId_ScreeningId",
                table: "ScreeningSeats",
                columns: new[] { "HeldByBookingId", "ScreeningId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningSeats_PhysicalSeat",
                table: "ScreeningSeats",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningSeats_ScreeningId_AuditoriumId",
                table: "ScreeningSeats",
                columns: new[] { "ScreeningId", "AuditoriumId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningSeats_SeatId_AuditoriumId",
                table: "ScreeningSeats",
                columns: new[] { "SeatId", "AuditoriumId" });

            migrationBuilder.CreateIndex(
                name: "UQ_ScreeningSeats_Seat",
                table: "ScreeningSeats",
                columns: new[] { "ScreeningId", "SeatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seats_Type",
                table: "Seats",
                column: "SeatTypeId");

            migrationBuilder.CreateIndex(
                name: "UQ_Seats_Position",
                table: "Seats",
                columns: new[] { "AuditoriumId", "RowLabel", "SeatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatTypes_TypeName",
                table: "SeatTypes",
                column: "TypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_StaffCode",
                table: "Staffs",
                column: "StaffCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_UserId",
                table: "Staffs",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Booking",
                table: "Tickets",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BookingId_ScreeningId",
                table: "Tickets",
                columns: new[] { "BookingId", "ScreeningId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ScreeningId",
                table: "Tickets",
                column: "ScreeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ScreeningSeatId_ScreeningId",
                table: "Tickets",
                columns: new[] { "ScreeningSeatId", "ScreeningId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketCode",
                table: "Tickets",
                column: "TicketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Tickets_ActiveSeat",
                table: "Tickets",
                column: "ScreeningSeatId",
                unique: true,
                filter: "[Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Screenings_Auditoriums_AuditoriumId",
                table: "Screenings",
                column: "AuditoriumId",
                principalTable: "Auditoriums",
                principalColumn: "AuditoriumId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Screenings_Auditoriums_AuditoriumId",
                table: "Screenings");

            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "BookingConcessions");

            migrationBuilder.DropTable(
                name: "CinemaManagers");

            migrationBuilder.DropTable(
                name: "MovieGenres");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "ConcessionItems");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "ScreeningSeats");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "Auditoriums");

            migrationBuilder.DropTable(
                name: "SeatTypes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Screenings_Room",
                table: "Screenings");

            migrationBuilder.DropIndex(
                name: "IX_Screenings_MovieStart",
                table: "Screenings");

            migrationBuilder.DropIndex(
                name: "IX_Screenings_RoomStart",
                table: "Screenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Screenings_Cleanup",
                table: "Screenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Screenings_Price",
                table: "Screenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Screenings_Status",
                table: "Screenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Screenings_TimeRange",
                table: "Screenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Movies_Duration",
                table: "Movies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Movies_Status",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "CleanupMinutes",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "EndAt",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "AgeRating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "TrailerUrl",
                table: "Movies");

            migrationBuilder.RenameColumn(
                name: "AuditoriumId",
                table: "Screenings",
                newName: "CinemaRoomId");

            migrationBuilder.RenameColumn(
                name: "ScreeningId",
                table: "Screenings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "MovieId",
                table: "Movies",
                newName: "Id");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAt",
                table: "Screenings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "StartsAt",
                table: "Screenings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Movies",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldUnicode: false,
                oldMaxLength: 20,
                oldDefaultValue: "Upcoming");

            migrationBuilder.AlterColumn<string>(
                name: "PosterUrl",
                table: "Movies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Movies",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "CinemaRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CinemaRooms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_CinemaRoomId_StartsAt",
                table: "Screenings",
                columns: new[] { "CinemaRoomId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_MovieId_StartsAt",
                table: "Screenings",
                columns: new[] { "MovieId", "StartsAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Screenings_TimeRange",
                table: "Screenings",
                sql: "[EndsAt] > [StartsAt]");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Title",
                table: "Movies",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_CinemaRooms_Name",
                table: "CinemaRooms",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Screenings_CinemaRooms_CinemaRoomId",
                table: "Screenings",
                column: "CinemaRoomId",
                principalTable: "CinemaRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
