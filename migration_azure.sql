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
    WHERE [MigrationId] = N'20260627123535_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260627123535_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627130112_AddDevice'
)
BEGIN
    CREATE TABLE [Devices] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Typ] nvarchar(50) NULL,
        [Standort] nvarchar(24) NULL,
        [Temperatur] float NULL,
        [RelativeFeuchte] float NULL,
        [Wassertank] bit NOT NULL,
        [ZuletztGesehen] datetime2 NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627130112_AddDevice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260627130112_AddDevice', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141619_UpdateDevice'
)
BEGIN
    ALTER TABLE [Devices] ADD [DeviceId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627141619_UpdateDevice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260627141619_UpdateDevice', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629112143_AddRaumKlimaLog'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Devices]') AND [c].[name] = N'RelativeFeuchte');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Devices] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Devices] DROP COLUMN [RelativeFeuchte];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629112143_AddRaumKlimaLog'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Devices]') AND [c].[name] = N'Temperatur');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Devices] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Devices] DROP COLUMN [Temperatur];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629112143_AddRaumKlimaLog'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Devices]') AND [c].[name] = N'Wassertank');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Devices] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Devices] DROP COLUMN [Wassertank];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629112143_AddRaumKlimaLog'
)
BEGIN
    EXEC sp_rename N'[Devices].[ZuletztGesehen]', N'ZuerstGesehen', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629112143_AddRaumKlimaLog'
)
BEGIN
    CREATE TABLE [RaumKlimaLogs] (
        [Id] int NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [Temperatur] float NULL,
        [RelativeFeuchte] float NULL,
        [Wassertank] bit NOT NULL,
        [Zeitstempel] datetime2 NOT NULL,
        CONSTRAINT [PK_RaumKlimaLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RaumKlimaLogs_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629112143_AddRaumKlimaLog'
)
BEGIN
    CREATE INDEX [IX_RaumKlimaLogs_DeviceId] ON [RaumKlimaLogs] ([DeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629112143_AddRaumKlimaLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260629112143_AddRaumKlimaLog', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709120516_AddCustomerIdToDevice'
)
BEGIN
    ALTER TABLE [Devices] ADD [CustomerId] nvarchar(16) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709120516_AddCustomerIdToDevice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709120516_AddCustomerIdToDevice', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709212632_AddCustomerProfile'
)
BEGIN
    CREATE TABLE [CustomerProfiles] (
        [Id] int NOT NULL IDENTITY,
        [OwnerId] nvarchar(500) NOT NULL,
        [CustomerId] nvarchar(16) NOT NULL,
        CONSTRAINT [PK_CustomerProfiles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709212632_AddCustomerProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709212632_AddCustomerProfile', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711103837_AddCaptionAndOwnerInfo'
)
BEGIN
    ALTER TABLE [Devices] ADD [Caption] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711103837_AddCaptionAndOwnerInfo'
)
BEGIN
    ALTER TABLE [CustomerProfiles] ADD [DisplayName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711103837_AddCaptionAndOwnerInfo'
)
BEGIN
    ALTER TABLE [CustomerProfiles] ADD [Email] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711103837_AddCaptionAndOwnerInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260711103837_AddCaptionAndOwnerInfo', N'10.0.9');
END;

COMMIT;
GO

