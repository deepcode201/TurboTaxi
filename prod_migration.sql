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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Locations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NULL,
        [AddressLine] nvarchar(max) NULL,
        [City] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Locations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [PromoCodes] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [DiscountType] int NOT NULL,
        [DiscountValue] decimal(18,2) NOT NULL,
        [MaxDiscountAmount] decimal(18,2) NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [MaxUsageCount] int NULL,
        [MaxUsagePerUser] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_PromoCodes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Tariffs] (
        [Id] int NOT NULL IDENTITY,
        [Country] nvarchar(max) NOT NULL,
        [City] nvarchar(max) NOT NULL,
        [VehicleType] int NOT NULL,
        [BaseFare] decimal(18,2) NOT NULL,
        [PricePerKm] decimal(18,2) NOT NULL,
        [PricePerMinute] decimal(18,2) NOT NULL,
        [MinimumFare] decimal(18,2) NOT NULL,
        [FreeKm] decimal(18,2) NULL,
        [FreeMinutes] int NULL,
        [NightMultiplier] decimal(18,2) NULL,
        [NightStart] time NULL,
        [NightEnd] time NULL,
        [CancellationFee] decimal(18,2) NULL,
        [WaitingPricePerMinute] decimal(18,2) NULL,
        [IsActive] bit NOT NULL,
        [ValidFrom] datetime2 NULL,
        [ValidTo] datetime2 NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Tariffs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(max) NOT NULL,
        [PhoneNumber] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Vehicles] (
        [Id] int NOT NULL IDENTITY,
        [PlateNumber] nvarchar(max) NOT NULL,
        [Brand] nvarchar(max) NOT NULL,
        [Model] nvarchar(max) NOT NULL,
        [Color] nvarchar(max) NOT NULL,
        [Year] int NOT NULL,
        [VehicleType] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [UserFavoriteLocations] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [LocationId] int NOT NULL,
        [AliasName] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_UserFavoriteLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserFavoriteLocations_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserFavoriteLocations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [UserPromoCodes] (
        [UserId] int NOT NULL,
        [PromoCodeId] int NOT NULL,
        [UsedCount] int NOT NULL,
        [LastUsedTime] datetime2 NULL,
        [Id] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_UserPromoCodes] PRIMARY KEY ([UserId], [PromoCodeId]),
        CONSTRAINT [FK_UserPromoCodes_PromoCodes_PromoCodeId] FOREIGN KEY ([PromoCodeId]) REFERENCES [PromoCodes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserPromoCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Drivers] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [DriverStatus] int NOT NULL,
        [IsVerified] bit NOT NULL,
        [VehicleId] int NULL,
        [LastKnownLatitude] float NULL,
        [LastKnownLongitude] float NULL,
        [LastLocationUpdateTime] datetime2 NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Drivers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Drivers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Drivers_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [DriverStatusHistories] (
        [Id] int NOT NULL IDENTITY,
        [DriverId] int NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [ChangedTime] datetime2 NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_DriverStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DriverStatusHistories_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [DriverId] int NULL,
        [Title] nvarchar(max) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadTime] datetime2 NULL,
        [NotificationType] int NOT NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Rides] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [DriverId] int NULL,
        [StartLocationId] int NOT NULL,
        [EndLocationId] int NOT NULL,
        [RequestedVehicleType] int NOT NULL,
        [TariffId] int NULL,
        [Status] int NOT NULL,
        [RequestedTime] datetime2 NULL,
        [AcceptedTime] datetime2 NULL,
        [PickUpTime] datetime2 NULL,
        [CompletedTime] datetime2 NULL,
        [CanceledTime] datetime2 NULL,
        [EstimatedDistanceKm] float NULL,
        [EstimatedDurationMinutes] int NULL,
        [EstimatedFare] decimal(18,2) NULL,
        [ActualDistanceKm] float NULL,
        [ActualDurationMinutes] int NULL,
        [ActualFare] decimal(18,2) NULL,
        [RoutePolyline] nvarchar(max) NULL,
        [PromoCodeId] int NULL,
        [DiscountAmount] decimal(18,2) NULL,
        [PaymentId] int NULL,
        [TariffId1] int NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Rides] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Rides_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Rides_Locations_EndLocationId] FOREIGN KEY ([EndLocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Rides_Locations_StartLocationId] FOREIGN KEY ([StartLocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Rides_PromoCodes_PromoCodeId] FOREIGN KEY ([PromoCodeId]) REFERENCES [PromoCodes] ([Id]),
        CONSTRAINT [FK_Rides_Tariffs_TariffId] FOREIGN KEY ([TariffId]) REFERENCES [Tariffs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Rides_Tariffs_TariffId1] FOREIGN KEY ([TariffId1]) REFERENCES [Tariffs] ([Id]),
        CONSTRAINT [FK_Rides_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [RideId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [PaymentStatus] int NOT NULL,
        [PaymentMethod] int NOT NULL,
        [PaidTime] datetime2 NULL,
        [ExternalTransactionId] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Rides_RideId] FOREIGN KEY ([RideId]) REFERENCES [Rides] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [Ratings] (
        [Id] int NOT NULL IDENTITY,
        [RideId] int NOT NULL,
        [FromUserId] int NOT NULL,
        [ToDriverId] int NULL,
        [ToUserId] int NULL,
        [Stars] int NOT NULL,
        [Comment] nvarchar(max) NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_Ratings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Ratings_Drivers_ToDriverId] FOREIGN KEY ([ToDriverId]) REFERENCES [Drivers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Ratings_Rides_RideId] FOREIGN KEY ([RideId]) REFERENCES [Rides] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Ratings_Users_FromUserId] FOREIGN KEY ([FromUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Ratings_Users_ToUserId] FOREIGN KEY ([ToUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE TABLE [RideStatusHistories] (
        [Id] int NOT NULL IDENTITY,
        [RideId] int NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [ChangedTime] datetime2 NOT NULL,
        [ChangedByType] nvarchar(max) NULL,
        [ChangedByUserId] int NULL,
        [CreatedTime] datetime2 NOT NULL,
        [UpdatedTime] datetime2 NULL,
        CONSTRAINT [PK_RideStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RideStatusHistories_Rides_RideId] FOREIGN KEY ([RideId]) REFERENCES [Rides] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Drivers_UserId] ON [Drivers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Drivers_VehicleId] ON [Drivers] ([VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_DriverStatusHistories_DriverId] ON [DriverStatusHistories] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Notifications_DriverId] ON [Notifications] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_RideId] ON [Payments] ([RideId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Ratings_FromUserId] ON [Ratings] ([FromUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Ratings_RideId] ON [Ratings] ([RideId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Ratings_ToDriverId] ON [Ratings] ([ToDriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Ratings_ToUserId] ON [Ratings] ([ToUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Rides_DriverId] ON [Rides] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Rides_EndLocationId] ON [Rides] ([EndLocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Rides_PromoCodeId] ON [Rides] ([PromoCodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Rides_StartLocationId] ON [Rides] ([StartLocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Rides_TariffId] ON [Rides] ([TariffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Rides_TariffId1] ON [Rides] ([TariffId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_Rides_UserId] ON [Rides] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_RideStatusHistories_RideId] ON [RideStatusHistories] ([RideId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_UserFavoriteLocations_LocationId] ON [UserFavoriteLocations] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_UserFavoriteLocations_UserId] ON [UserFavoriteLocations] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    CREATE INDEX [IX_UserPromoCodes_PromoCodeId] ON [UserPromoCodes] ([PromoCodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124155504_newDbAdded'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251124155504_newDbAdded', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124174642_newDbAdded1'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'LastKnownLatitude');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Drivers] DROP COLUMN [LastKnownLatitude];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124174642_newDbAdded1'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'LastKnownLongitude');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Drivers] DROP COLUMN [LastKnownLongitude];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124174642_newDbAdded1'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'LastLocationUpdateTime');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Drivers] DROP COLUMN [LastLocationUpdateTime];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124174642_newDbAdded1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251124174642_newDbAdded1', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124175004_newDbAdded2'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'DriverStatus');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Drivers] DROP COLUMN [DriverStatus];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124175004_newDbAdded2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251124175004_newDbAdded2', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251203101805_ProdInit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251203101805_ProdInit', N'10.0.0');
END;

COMMIT;
GO

