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
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [Plans] (
        [Id] int NOT NULL IDENTITY,
        [Code] varchar(20) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [MonthlyPrice] decimal(10,2) NOT NULL,
        [Currency] varchar(3) NOT NULL,
        [MaxServices] int NOT NULL,
        [MaxCounters] int NOT NULL,
        [MaxStaff] int NOT NULL,
        [MaxTicketsPerMonth] int NOT NULL,
        [HasDisplayScreen] bit NOT NULL,
        [HasPushAlerts] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Plans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Slug] varchar(40) NOT NULL,
        [Kind] int NOT NULL,
        [Phone] varchar(20) NULL,
        [City] nvarchar(60) NULL,
        [Address] nvarchar(200) NULL,
        [TimeZoneId] varchar(40) NOT NULL,
        [AlertLeadMinutes] int NOT NULL,
        [AcceptingTickets] bit NOT NULL,
        [IsSuspended] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [PlanId] int NOT NULL,
        [SubscriptionStatus] int NOT NULL,
        [TrialEndsAt] datetime2 NULL,
        [CurrentPeriodEnd] datetime2 NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tenants_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [Counters] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [IsOpen] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Counters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Counters_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [PlanId] int NOT NULL,
        [Number] varchar(20) NOT NULL,
        [Amount] decimal(10,2) NOT NULL,
        [Currency] varchar(3) NOT NULL,
        [Months] int NOT NULL,
        [Status] tinyint NOT NULL,
        [PaymentReference] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [PaidAt] datetime2 NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [Services] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Description] nvarchar(250) NULL,
        [Prefix] varchar(2) NOT NULL,
        [AvgServiceSeconds] int NOT NULL,
        [DailyLimit] int NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Services] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Services_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [CounterServices] (
        [CounterId] uniqueidentifier NOT NULL,
        [ServiceId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CounterServices] PRIMARY KEY ([CounterId], [ServiceId]),
        CONSTRAINT [FK_CounterServices_Counters_CounterId] FOREIGN KEY ([CounterId]) REFERENCES [Counters] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CounterServices_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [DailySequences] (
        [ServiceId] uniqueidentifier NOT NULL,
        [ServiceDate] date NOT NULL,
        [LastNumber] int NOT NULL,
        CONSTRAINT [PK_DailySequences] PRIMARY KEY ([ServiceId], [ServiceDate]),
        CONSTRAINT [FK_DailySequences_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [ServiceId] uniqueidentifier NOT NULL,
        [ServiceDate] date NOT NULL,
        [Number] int NOT NULL,
        [Code] varchar(8) NOT NULL,
        [PublicToken] varchar(32) NOT NULL,
        [CustomerName] nvarchar(80) NULL,
        [CustomerPhone] varchar(20) NULL,
        [Source] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        [CounterId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CalledAt] datetime2 NULL,
        [ServiceStartedAt] datetime2 NULL,
        [FinishedAt] datetime2 NULL,
        [AlertSentAt] datetime2 NULL,
        [RecallCount] int NOT NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tickets_Counters_CounterId] FOREIGN KEY ([CounterId]) REFERENCES [Counters] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketPushSubscriptions] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] bigint NOT NULL,
        [Endpoint] varchar(600) NOT NULL,
        [P256dh] varchar(120) NOT NULL,
        [Auth] varchar(60) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TicketPushSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketPushSubscriptions_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] ON;
    EXEC(N'INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (''8c1a3f0e-4b2d-4f6a-9a51-0d7e2b1c9f01'', N''role-platform-admin'', N''PlatformAdmin'', N''PLATFORMADMIN''),
    (''8c1a3f0e-4b2d-4f6a-9a51-0d7e2b1c9f02'', N''role-owner'', N''Owner'', N''OWNER''),
    (''8c1a3f0e-4b2d-4f6a-9a51-0d7e2b1c9f03'', N''role-staff'', N''Staff'', N''STAFF'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Currency', N'HasDisplayScreen', N'HasPushAlerts', N'IsActive', N'MaxCounters', N'MaxServices', N'MaxStaff', N'MaxTicketsPerMonth', N'MonthlyPrice', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] ON;
    EXEC(N'INSERT INTO [Plans] ([Id], [Code], [Currency], [HasDisplayScreen], [HasPushAlerts], [IsActive], [MaxCounters], [MaxServices], [MaxStaff], [MaxTicketsPerMonth], [MonthlyPrice], [Name], [SortOrder])
    VALUES (1, ''starter'', ''EGP'', CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), 2, 3, 3, 3000, 499.0, N''الأساسية'', 1),
    (2, ''pro'', ''EGP'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), 6, 10, 10, 15000, 999.0, N''الاحترافية'', 2),
    (3, ''business'', ''EGP'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), 30, 50, 50, 100000, 2499.0, N''المؤسسات'', 3)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Currency', N'HasDisplayScreen', N'HasPushAlerts', N'IsActive', N'MaxCounters', N'MaxServices', N'MaxStaff', N'MaxTicketsPerMonth', N'MonthlyPrice', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_TenantId] ON [AspNetUsers] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Counters_TenantId_IsActive] ON [Counters] ([TenantId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterServices_ServiceId] ON [CounterServices] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_Number] ON [Invoices] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_PlanId] ON [Invoices] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Invoices_Status] ON [Invoices] ([Status]) WHERE [Status] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_TenantId_CreatedAt] ON [Invoices] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Plans_Code] ON [Plans] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Services_TenantId_IsActive_SortOrder] ON [Services] ([TenantId], [IsActive], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_PlanId] ON [Tenants] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_Slug] ON [Tenants] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TicketPushSubscriptions_TicketId] ON [TicketPushSubscriptions] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Tickets_CounterId_Status] ON [Tickets] ([CounterId], [Status]) WHERE [CounterId] IS NOT NULL AND [Status] IN (1, 2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Tickets_PendingAlerts] ON [Tickets] ([ServiceDate], [ServiceId]) WHERE [Status] = 0 AND [AlertSentAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tickets_PublicToken] ON [Tickets] ([PublicToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tickets_ServiceId_ServiceDate_Number] ON [Tickets] ([ServiceId], [ServiceDate], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_ServiceId_ServiceDate_Status_Id] ON [Tickets] ([ServiceId], [ServiceDate], [Status], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_TenantId_CreatedAt] ON [Tickets] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_TenantId_ServiceDate_Status] ON [Tickets] ([TenantId], [ServiceDate], [Status]) INCLUDE ([ServiceId], [CounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Tickets_ActivePhone] ON [Tickets] ([ServiceId], [ServiceDate], [CustomerPhone]) WHERE [CustomerPhone] IS NOT NULL AND [Status] IN (0, 1, 2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030220757_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030220757_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

