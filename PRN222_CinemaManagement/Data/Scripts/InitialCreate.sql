IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Auditoriums] (
        [AuditoriumId] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [TotalRows] int NOT NULL,
        [TotalColumns] int NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Active',
        CONSTRAINT [PK_Auditoriums] PRIMARY KEY ([AuditoriumId]),
        CONSTRAINT [CK_Auditoriums_Columns] CHECK ([TotalColumns] > 0),
        CONSTRAINT [CK_Auditoriums_Rows] CHECK ([TotalRows] > 0),
        CONSTRAINT [CK_Auditoriums_Status] CHECK ([Status] IN ('Active','Maintenance','Inactive'))
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [ConcessionItems] (
        [ConcessionItemId] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(255) NULL,
        [Price] decimal(18,2) NOT NULL,
        [StockQuantity] int NOT NULL DEFAULT 0,
        [LowStockThreshold] int NOT NULL DEFAULT 10,
        [Status] varchar(20) NOT NULL DEFAULT 'Active',
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ConcessionItems] PRIMARY KEY ([ConcessionItemId]),
        CONSTRAINT [CK_Concessions_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Concessions_Status] CHECK ([Status] IN ('Active','Inactive')),
        CONSTRAINT [CK_Concessions_Stock] CHECK ([StockQuantity] >= 0),
        CONSTRAINT [CK_Concessions_Threshold] CHECK ([LowStockThreshold] >= 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Genres] (
        [GenreId] int NOT NULL IDENTITY,
        [GenreName] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        CONSTRAINT [PK_Genres] PRIMARY KEY ([GenreId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Movies] (
        [MovieId] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [DurationMinutes] int NOT NULL,
        [AgeRating] varchar(10) NOT NULL DEFAULT 'P',
        [ReleaseDate] date NULL,
        [PosterUrl] nvarchar(1000) NULL,
        [TrailerUrl] nvarchar(1000) NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Upcoming',
        CONSTRAINT [PK_Movies] PRIMARY KEY ([MovieId]),
        CONSTRAINT [CK_Movies_Duration] CHECK ([DurationMinutes] > 0),
        CONSTRAINT [CK_Movies_Status] CHECK ([Status] IN ('Upcoming','Showing','Ended'))
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [RoleId] int NOT NULL IDENTITY,
        [RoleName] nvarchar(50) NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [SeatTypes] (
        [SeatTypeId] int NOT NULL IDENTITY,
        [TypeName] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [PriceMultiplier] decimal(6,3) NOT NULL DEFAULT 1.0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_SeatTypes] PRIMARY KEY ([SeatTypeId]),
        CONSTRAINT [CK_SeatTypes_Multiplier] CHECK ([PriceMultiplier] > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [MovieGenres] (
        [MovieId] int NOT NULL,
        [GenreId] int NOT NULL,
        CONSTRAINT [PK_MovieGenres] PRIMARY KEY ([MovieId], [GenreId]),
        CONSTRAINT [FK_MovieGenres_Genres_GenreId] FOREIGN KEY ([GenreId]) REFERENCES [Genres] ([GenreId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MovieGenres_Movies_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movies] ([MovieId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Screenings] (
        [ScreeningId] int NOT NULL IDENTITY,
        [MovieId] int NOT NULL,
        [AuditoriumId] int NOT NULL,
        [StartAt] datetime2(0) NOT NULL,
        [EndAt] datetime2(0) NOT NULL,
        [CleanupMinutes] int NOT NULL DEFAULT 15,
        [BasePrice] decimal(18,2) NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Scheduled',
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Screenings] PRIMARY KEY ([ScreeningId]),
        CONSTRAINT [UQ_Screenings_Room] UNIQUE ([ScreeningId], [AuditoriumId]),
        CONSTRAINT [CK_Screenings_Cleanup] CHECK ([CleanupMinutes] = 15),
        CONSTRAINT [CK_Screenings_Price] CHECK ([BasePrice] >= 0),
        CONSTRAINT [CK_Screenings_Status] CHECK ([Status] IN ('Scheduled','Ongoing','Finished','Cancelled')),
        CONSTRAINT [CK_Screenings_TimeRange] CHECK ([EndAt] > [StartAt]),
        CONSTRAINT [FK_Screenings_Auditoriums_AuditoriumId] FOREIGN KEY ([AuditoriumId]) REFERENCES [Auditoriums] ([AuditoriumId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Screenings_Movies_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movies] ([MovieId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [UserId] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [Username] nvarchar(100) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [PhoneNumber] varchar(20) NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Active',
        [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
        CONSTRAINT [CK_Users_Status] CHECK ([Status] IN ('Active','Inactive','Locked')),
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Seats] (
        [SeatId] int NOT NULL IDENTITY,
        [AuditoriumId] int NOT NULL,
        [SeatTypeId] int NOT NULL,
        [RowLabel] varchar(10) NOT NULL,
        [SeatNumber] int NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Active',
        CONSTRAINT [PK_Seats] PRIMARY KEY ([SeatId]),
        CONSTRAINT [UQ_Seats_Room] UNIQUE ([SeatId], [AuditoriumId]),
        CONSTRAINT [CK_Seats_Number] CHECK ([SeatNumber] > 0),
        CONSTRAINT [CK_Seats_Status] CHECK ([Status] IN ('Active','Broken','Inactive')),
        CONSTRAINT [FK_Seats_Auditoriums_AuditoriumId] FOREIGN KEY ([AuditoriumId]) REFERENCES [Auditoriums] ([AuditoriumId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Seats_SeatTypes_SeatTypeId] FOREIGN KEY ([SeatTypeId]) REFERENCES [SeatTypes] ([SeatTypeId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Admins] (
        [AdminId] int NOT NULL IDENTITY,
        [AdminCode] varchar(20) NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_Admins] PRIMARY KEY ([AdminId]),
        CONSTRAINT [FK_Admins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [CinemaManagers] (
        [ManagerId] int NOT NULL IDENTITY,
        [ManagerCode] varchar(20) NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_CinemaManagers] PRIMARY KEY ([ManagerId]),
        CONSTRAINT [FK_CinemaManagers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [CustomerId] int NOT NULL IDENTITY,
        [CustomerCode] varchar(20) NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId]),
        CONSTRAINT [FK_Customers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Staffs] (
        [StaffId] int NOT NULL IDENTITY,
        [StaffCode] varchar(20) NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_Staffs] PRIMARY KEY ([StaffId]),
        CONSTRAINT [FK_Staffs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Bookings] (
        [BookingId] int NOT NULL IDENTITY,
        [BookingCode] varchar(40) NOT NULL,
        [CustomerId] int NULL,
        [StaffId] int NULL,
        [ScreeningId] int NOT NULL,
        [BookingDate] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [ExpiresAt] datetime2(0) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [BookingChannel] varchar(20) NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Pending',
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([BookingId]),
        CONSTRAINT [UQ_Bookings_Show] UNIQUE ([BookingId], [ScreeningId]),
        CONSTRAINT [CK_Bookings_Channel] CHECK ([BookingChannel] IN ('Online','Counter')),
        CONSTRAINT [CK_Bookings_ChannelActors] CHECK (([BookingChannel] = 'Online' AND [CustomerId] IS NOT NULL AND [StaffId] IS NULL) OR ([BookingChannel] = 'Counter' AND [StaffId] IS NOT NULL)),
        CONSTRAINT [CK_Bookings_Expiry] CHECK ([ExpiresAt] > [BookingDate]),
        CONSTRAINT [CK_Bookings_Status] CHECK ([Status] IN ('Pending','Confirmed','Cancelled','Expired','Refunded')),
        CONSTRAINT [CK_Bookings_Total] CHECK ([TotalAmount] >= 0),
        CONSTRAINT [FK_Bookings_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Screenings_ScreeningId] FOREIGN KEY ([ScreeningId]) REFERENCES [Screenings] ([ScreeningId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Staffs_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staffs] ([StaffId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [BookingConcessions] (
        [BookingConcessionId] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [ConcessionItemId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Subtotal] AS CONVERT(decimal(18,2), [Quantity] * [UnitPrice]) PERSISTED,
        CONSTRAINT [PK_BookingConcessions] PRIMARY KEY ([BookingConcessionId]),
        CONSTRAINT [CK_BookingConcessions_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_BookingConcessions_UnitPrice] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [FK_BookingConcessions_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([BookingId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BookingConcessions_ConcessionItems_ConcessionItemId] FOREIGN KEY ([ConcessionItemId]) REFERENCES [ConcessionItems] ([ConcessionItemId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [PaymentId] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [PaymentCode] varchar(50) NOT NULL,
        [Provider] varchar(30) NOT NULL,
        [TransactionRef] varchar(100) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [RefundAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [PaymentMethod] varchar(30) NOT NULL,
        [PaymentStatus] varchar(20) NOT NULL DEFAULT 'Pending',
        [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [PaidAt] datetime2(0) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [CK_Payments_Method] CHECK ([PaymentMethod] IN ('Cash','VNPay_Simulated','MoMo_Simulated')),
        CONSTRAINT [CK_Payments_PaidState] CHECK (([PaymentStatus] = 'Success' AND [PaidAt] IS NOT NULL) OR ([PaymentStatus] <> 'Success' AND [PaidAt] IS NULL)),
        CONSTRAINT [CK_Payments_RefundRange] CHECK ([RefundAmount] >= 0 AND [RefundAmount] <= [Amount]),
        CONSTRAINT [CK_Payments_RefundSuccess] CHECK ([RefundAmount] = 0 OR [PaymentStatus] = 'Success'),
        CONSTRAINT [CK_Payments_Status] CHECK ([PaymentStatus] IN ('Pending','Success','Failed','Cancelled')),
        CONSTRAINT [FK_Payments_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([BookingId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [ScreeningSeats] (
        [ScreeningSeatId] int NOT NULL IDENTITY,
        [ScreeningId] int NOT NULL,
        [SeatId] int NOT NULL,
        [AuditoriumId] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Available',
        [HeldByBookingId] int NULL,
        [HoldExpiresAt] datetime2(0) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ScreeningSeats] PRIMARY KEY ([ScreeningSeatId]),
        CONSTRAINT [UQ_ScreeningSeats_Show] UNIQUE ([ScreeningSeatId], [ScreeningId]),
        CONSTRAINT [CK_ScreeningSeats_HeldState] CHECK (([Status] = 'Held' AND [HeldByBookingId] IS NOT NULL AND [HoldExpiresAt] IS NOT NULL) OR ([Status] <> 'Held' AND [HeldByBookingId] IS NULL AND [HoldExpiresAt] IS NULL)),
        CONSTRAINT [CK_ScreeningSeats_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_ScreeningSeats_Status] CHECK ([Status] IN ('Available','Held','Sold','Blocked')),
        CONSTRAINT [FK_ScreeningSeats_Auditoriums_AuditoriumId] FOREIGN KEY ([AuditoriumId]) REFERENCES [Auditoriums] ([AuditoriumId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ScreeningSeats_Bookings_HeldByBookingId_ScreeningId] FOREIGN KEY ([HeldByBookingId], [ScreeningId]) REFERENCES [Bookings] ([BookingId], [ScreeningId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScreeningSeats_Seats_SeatId_AuditoriumId] FOREIGN KEY ([SeatId], [AuditoriumId]) REFERENCES [Seats] ([SeatId], [AuditoriumId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScreeningSeats_Screenings_ScreeningId_AuditoriumId] FOREIGN KEY ([ScreeningId], [AuditoriumId]) REFERENCES [Screenings] ([ScreeningId], [AuditoriumId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [TicketId] int NOT NULL IDENTITY,
        [TicketCode] varchar(50) NOT NULL,
        [BookingId] int NOT NULL,
        [ScreeningSeatId] int NOT NULL,
        [ScreeningId] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Valid',
        [IssuedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CheckedInAt] datetime2(0) NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([TicketId]),
        CONSTRAINT [CK_Tickets_CheckInState] CHECK (([Status] = 'CheckedIn' AND [CheckedInAt] IS NOT NULL) OR ([Status] <> 'CheckedIn' AND [CheckedInAt] IS NULL)),
        CONSTRAINT [CK_Tickets_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Tickets_Status] CHECK ([Status] IN ('Valid','CheckedIn','Cancelled')),
        CONSTRAINT [FK_Tickets_Bookings_BookingId_ScreeningId] FOREIGN KEY ([BookingId], [ScreeningId]) REFERENCES [Bookings] ([BookingId], [ScreeningId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_ScreeningSeats_ScreeningSeatId_ScreeningId] FOREIGN KEY ([ScreeningSeatId], [ScreeningId]) REFERENCES [ScreeningSeats] ([ScreeningSeatId], [ScreeningId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_Screenings_ScreeningId] FOREIGN KEY ([ScreeningId]) REFERENCES [Screenings] ([ScreeningId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ConcessionItemId', N'Description', N'LowStockThreshold', N'Name', N'Price', N'Status', N'StockQuantity') AND [object_id] = OBJECT_ID(N'[ConcessionItems]'))
        SET IDENTITY_INSERT [ConcessionItems] ON;
    EXEC(N'INSERT INTO [ConcessionItems] ([ConcessionItemId], [Description], [LowStockThreshold], [Name], [Price], [Status], [StockQuantity])
    VALUES (1, N''Bắp caramel'', 10, N''Popcorn Caramel'', 65000.0, ''Active'', 100),
    (2, N''Nước ngọt'', 10, N''Coca-Cola 32oz'', 30000.0, ''Active'', 100),
    (3, N''Combo quản lý tồn độc lập'', 10, N''Combo Solo'', 85000.0, ''Active'', 50)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ConcessionItemId', N'Description', N'LowStockThreshold', N'Name', N'Price', N'Status', N'StockQuantity') AND [object_id] = OBJECT_ID(N'[ConcessionItems]'))
        SET IDENTITY_INSERT [ConcessionItems] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GenreId', N'Description', N'GenreName') AND [object_id] = OBJECT_ID(N'[Genres]'))
        SET IDENTITY_INSERT [Genres] ON;
    EXEC(N'INSERT INTO [Genres] ([GenreId], [Description], [GenreName])
    VALUES (1, NULL, N''Hành động''),
    (2, NULL, N''Tình cảm''),
    (3, NULL, N''Hoạt hình''),
    (4, NULL, N''Kinh dị''),
    (5, NULL, N''Khoa học viễn tưởng'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GenreId', N'Description', N'GenreName') AND [object_id] = OBJECT_ID(N'[Genres]'))
        SET IDENTITY_INSERT [Genres] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Description', N'RoleName') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([RoleId], [Description], [RoleName])
    VALUES (1, N''System Administrator'', N''Admin''),
    (2, N''Cinema Operations Manager'', N''CinemaManager''),
    (3, N''Box-office and Counter Staff'', N''Staff''),
    (4, N''Moviegoer Customer'', N''Customer'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Description', N'RoleName') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'SeatTypeId', N'Description', N'IsActive', N'PriceMultiplier', N'TypeName') AND [object_id] = OBJECT_ID(N'[SeatTypes]'))
        SET IDENTITY_INSERT [SeatTypes] ON;
    EXEC(N'INSERT INTO [SeatTypes] ([SeatTypeId], [Description], [IsActive], [PriceMultiplier], [TypeName])
    VALUES (1, N''Ghế thường'', CAST(1 AS bit), 1.0, N''Standard''),
    (2, N''Ghế VIP'', CAST(1 AS bit), 1.2, N''VIP''),
    (3, N''Một đơn vị ghế đôi cho hai người, một vé'', CAST(1 AS bit), 2.0, N''Couple'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'SeatTypeId', N'Description', N'IsActive', N'PriceMultiplier', N'TypeName') AND [object_id] = OBJECT_ID(N'[SeatTypes]'))
        SET IDENTITY_INSERT [SeatTypes] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Admins_AdminCode] ON [Admins] ([AdminCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Admins_UserId] ON [Admins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Auditoriums_Name] ON [Auditoriums] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookingConcessions_Item] ON [BookingConcessions] ([ConcessionItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_BookingConcessions_Item] ON [BookingConcessions] ([BookingId], [ConcessionItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bookings_BookingCode] ON [Bookings] ([BookingCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_CustomerDate] ON [Bookings] ([CustomerId], [BookingDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_Show] ON [Bookings] ([ScreeningId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_Staff] ON [Bookings] ([StaffId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CinemaManagers_ManagerCode] ON [CinemaManagers] ([ManagerCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CinemaManagers_UserId] ON [CinemaManagers] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ConcessionItems_Name] ON [ConcessionItems] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_CustomerCode] ON [Customers] ([CustomerCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_UserId] ON [Customers] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Genres_GenreName] ON [Genres] ([GenreName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MovieGenres_Genre] ON [MovieGenres] ([GenreId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Booking] ON [Payments] ([BookingId]) WHERE [PaymentStatus] = ''Success''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_PaymentCode] ON [Payments] ([PaymentCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Payments_ProviderRef] ON [Payments] ([Provider], [TransactionRef]) WHERE [TransactionRef] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_RoleName] ON [Roles] ([RoleName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Seats_Type] ON [Seats] ([SeatTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Seats_Position] ON [Seats] ([AuditoriumId], [RowLabel], [SeatNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SeatTypes_TypeName] ON [SeatTypes] ([TypeName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Screenings_MovieStart] ON [Screenings] ([MovieId], [StartAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Screenings_RoomStart] ON [Screenings] ([AuditoriumId], [StartAt]) INCLUDE ([EndAt], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScreeningSeats_AuditoriumId] ON [ScreeningSeats] ([AuditoriumId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScreeningSeats_Booking] ON [ScreeningSeats] ([HeldByBookingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScreeningSeats_Expiry] ON [ScreeningSeats] ([Status], [HoldExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScreeningSeats_HeldByBookingId_ScreeningId] ON [ScreeningSeats] ([HeldByBookingId], [ScreeningId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScreeningSeats_PhysicalSeat] ON [ScreeningSeats] ([SeatId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScreeningSeats_SeatId_AuditoriumId] ON [ScreeningSeats] ([SeatId], [AuditoriumId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScreeningSeats_ScreeningId_AuditoriumId] ON [ScreeningSeats] ([ScreeningId], [AuditoriumId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ScreeningSeats_Seat] ON [ScreeningSeats] ([ScreeningId], [SeatId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staffs_StaffCode] ON [Staffs] ([StaffCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staffs_UserId] ON [Staffs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_Booking] ON [Tickets] ([BookingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_BookingId_ScreeningId] ON [Tickets] ([BookingId], [ScreeningId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_ScreeningId] ON [Tickets] ([ScreeningId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_ScreeningSeatId_ScreeningId] ON [Tickets] ([ScreeningSeatId], [ScreeningId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tickets_TicketCode] ON [Tickets] ([TicketCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Tickets_ActiveSeat] ON [Tickets] ([ScreeningSeatId]) WHERE [Status] <> ''Cancelled''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_Role] ON [Users] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102231823_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102231823_InitialCreate', N'8.0.33');
END;
GO

COMMIT;
GO

