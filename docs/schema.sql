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
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [LastLoginAt] datetimeoffset NULL,
        [IsActive] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(254) NOT NULL,
        [NormalizedEmail] nvarchar(254) NOT NULL,
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
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE TABLE [SecurityRateLimits] (
        [Id] nvarchar(80) NOT NULL,
        [WindowStart] datetimeoffset NOT NULL,
        [LastRequestAt] datetimeoffset NOT NULL,
        [Count] int NOT NULL,
        CONSTRAINT [PK_SecurityRateLimits] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE TABLE [EmailOtps] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Purpose] int NOT NULL,
        [OtpHash] varbinary(32) NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [AttemptCount] int NOT NULL,
        [MaxAttempts] int NOT NULL,
        [IsUsed] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UsedAt] datetimeoffset NULL,
        CONSTRAINT [PK_EmailOtps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailOtps_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE TABLE [PasswordResetTickets] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [Purpose] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [UsedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PasswordResetTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasswordResetTickets_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [RevokedAt] datetimeoffset NULL,
        [ReplacedByTokenId] uniqueidentifier NULL,
        [DeviceInfo] nvarchar(256) NULL,
        [IpAddress] nvarchar(45) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RefreshTokens_RefreshTokens_ReplacedByTokenId] FOREIGN KEY ([ReplacedByTokenId]) REFERENCES [RefreshTokens] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE TABLE [SecurityAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [EventType] int NOT NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(256) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [IsSuccessful] bit NOT NULL,
        [Metadata] nvarchar(100) NULL,
        CONSTRAINT [PK_SecurityAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SecurityAuditLogs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE UNIQUE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_EmailOtps_ExpiresAt] ON [EmailOtps] ([ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmailOtps_UserId_Purpose] ON [EmailOtps] ([UserId], [Purpose]) WHERE [IsUsed] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_EmailOtps_UserId_Purpose_CreatedAt] ON [EmailOtps] ([UserId], [Purpose], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PasswordResetTickets_TokenHash] ON [PasswordResetTickets] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTickets_UserId_ExpiresAt] ON [PasswordResetTickets] ([UserId], [ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_ExpiresAt] ON [RefreshTokens] ([ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_ReplacedByTokenId] ON [RefreshTokens] ([ReplacedByTokenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId_FamilyId_RevokedAt] ON [RefreshTokens] ([UserId], [FamilyId], [RevokedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_SecurityAuditLogs_CreatedAt] ON [SecurityAuditLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_SecurityAuditLogs_UserId_CreatedAt] ON [SecurityAuditLogs] ([UserId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    CREATE INDEX [IX_SecurityRateLimits_LastRequestAt] ON [SecurityRateLimits] ([LastRequestAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134858_InitialIdentityMigration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003134858_InitialIdentityMigration', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003135350_AddPasswordNotificationOutbox'
)
BEGIN
    CREATE TABLE [PasswordChangeNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [Attempts] int NOT NULL,
        [SentAt] datetimeoffset NULL,
        CONSTRAINT [PK_PasswordChangeNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasswordChangeNotifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003135350_AddPasswordNotificationOutbox'
)
BEGIN
    CREATE INDEX [IX_PasswordChangeNotifications_SentAt_NextAttemptAt] ON [PasswordChangeNotifications] ([SentAt], [NextAttemptAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003135350_AddPasswordNotificationOutbox'
)
BEGIN
    CREATE INDEX [IX_PasswordChangeNotifications_UserId] ON [PasswordChangeNotifications] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003135350_AddPasswordNotificationOutbox'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003135350_AddPasswordNotificationOutbox', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [AvailabilityExceptions] (
        [Id] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [StartMinute] int NOT NULL,
        [EndMinute] int NOT NULL,
        [IsAvailable] bit NOT NULL,
        [Note] nvarchar(300) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AvailabilityExceptions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_AvailabilityExceptions_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_AvailabilityExceptions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [FixedEvents] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Start] datetimeoffset NOT NULL,
        [End] datetimeoffset NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_FixedEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_FixedEvents_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_FixedEvents_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [InAppNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [Kind] nvarchar(60) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [DedupeKey] nvarchar(200) NOT NULL,
        [TaskId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_InAppNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_InAppNotifications_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_InAppNotifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [PlanProposals] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [Revision] uniqueidentifier NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [IsApplied] bit NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PlanProposals] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_PlanProposals_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_PlanProposals_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [TaskCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Color] nvarchar(7) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TaskCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TaskCategories_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_TaskCategories_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [WeeklyAvailability] (
        [Id] uniqueidentifier NOT NULL,
        [DayOfWeek] int NOT NULL,
        [StartMinute] int NOT NULL,
        [EndMinute] int NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_WeeklyAvailability] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_WeeklyAvailability_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_WeeklyAvailability_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [WorkPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [MinimumSessionMinutes] int NOT NULL,
        [MaximumSessionMinutes] int NOT NULL,
        [BreakMinutes] int NOT NULL,
        [PreferredStartMinute] int NOT NULL,
        [PreferredEndMinute] int NOT NULL,
        [Revision] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_WorkPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_WorkPreferences_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_WorkPreferences_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [WorkTasks] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [CategoryId] uniqueidentifier NULL,
        [ParentId] uniqueidentifier NULL,
        [Deadline] datetimeoffset NOT NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [Progress] int NOT NULL,
        [EstimatedMinutes] int NOT NULL,
        [TaskType] nvarchar(60) NOT NULL,
        [Splittable] bit NOT NULL,
        [MinimumSessionMinutes] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_WorkTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_WorkTasks_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_WorkTasks_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_TaskCategories_UserId_CategoryId] FOREIGN KEY ([UserId], [CategoryId]) REFERENCES [TaskCategories] ([UserId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_WorkTasks_UserId_ParentId] FOREIGN KEY ([UserId], [ParentId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [TaskDependencies] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [PredecessorId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TaskDependencies] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TaskDependencies_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_TaskDependencies_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskDependencies_WorkTasks_UserId_PredecessorId] FOREIGN KEY ([UserId], [PredecessorId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskDependencies_WorkTasks_UserId_TaskId] FOREIGN KEY ([UserId], [TaskId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE TABLE [WorkSessions] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [Start] datetimeoffset NOT NULL,
        [End] datetimeoffset NOT NULL,
        [Status] int NOT NULL,
        [IsLocked] bit NOT NULL,
        [ActualSeconds] bigint NOT NULL,
        [SegmentStartedAt] datetimeoffset NULL,
        [CompletedAt] datetimeoffset NULL,
        [ReasonCode] nvarchar(80) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_WorkSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_WorkSessions_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_WorkSessions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkSessions_WorkTasks_UserId_TaskId] FOREIGN KEY ([UserId], [TaskId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_AvailabilityExceptions_UserId] ON [AvailabilityExceptions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_AvailabilityExceptions_UserId_Date] ON [AvailabilityExceptions] ([UserId], [Date]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_FixedEvents_UserId] ON [FixedEvents] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_FixedEvents_UserId_Start] ON [FixedEvents] ([UserId], [Start]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_InAppNotifications_UserId] ON [InAppNotifications] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_InAppNotifications_UserId_CreatedAt] ON [InAppNotifications] ([UserId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InAppNotifications_UserId_DedupeKey] ON [InAppNotifications] ([UserId], [DedupeKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_PlanProposals_ExpiresAt] ON [PlanProposals] ([ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_PlanProposals_UserId] ON [PlanProposals] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_TaskCategories_UserId] ON [TaskCategories] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskCategories_UserId_Name] ON [TaskCategories] ([UserId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_TaskDependencies_UserId] ON [TaskDependencies] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_TaskDependencies_UserId_PredecessorId] ON [TaskDependencies] ([UserId], [PredecessorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskDependencies_UserId_TaskId_PredecessorId] ON [TaskDependencies] ([UserId], [TaskId], [PredecessorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WeeklyAvailability_UserId] ON [WeeklyAvailability] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WeeklyAvailability_UserId_DayOfWeek] ON [WeeklyAvailability] ([UserId], [DayOfWeek]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WorkPreferences_UserId] ON [WorkPreferences] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkSessions_UserId] ON [WorkSessions] ([UserId]) WHERE [Status] IN (1, 2)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WorkSessions_UserId_Start] ON [WorkSessions] ([UserId], [Start]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WorkSessions_UserId_TaskId] ON [WorkSessions] ([UserId], [TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_UserId] ON [WorkTasks] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_UserId_CategoryId] ON [WorkTasks] ([UserId], [CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_UserId_Deadline] ON [WorkTasks] ([UserId], [Deadline]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_UserId_ParentId] ON [WorkTasks] ([UserId], [ParentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004104534_AddWorkloadSystem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004104534_AddWorkloadSystem', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkTasks] ADD [AdjustedMinutes] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkTasks] ADD [AdjustmentFactor] float NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkTasks] ADD [CreationSource] nvarchar(16) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkTasks] ADD [DeletedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkTasks] ADD [IsHardDeadline] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkSessions] ADD [CreatedAt] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkSessions] ADD [SkipReason] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkSessions] ADD [SourceItemId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkSessions] ADD [UpdatedAt] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkPreferences] ADD [PlanningHorizonDays] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkPreferences] ADD [PreferredPeriod] nvarchar(10) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [TaskDependencies] ADD [CreatedAt] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD [AppliedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD [HorizonEnd] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD [HorizonStart] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD [Kind] nvarchar(10) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD [PreviousPlanId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD [Status] nvarchar(12) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD [TriggerEvent] nvarchar(24) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [InAppNotifications] ADD [PlanId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [InAppNotifications] ADD [ScheduledFor] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [InAppNotifications] ADD [SessionId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [FixedEvents] ADD [Description] nvarchar(1000) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [FixedEvents] ADD [EventType] nvarchar(12) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [account_status_logs] (
        [Id] uniqueidentifier NOT NULL,
        [AdminId] uniqueidentifier NOT NULL,
        [Action] nvarchar(10) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_account_status_logs] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_account_status_logs_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_account_status_logs_AspNetUsers_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_account_status_logs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [capacity_snapshots] (
        [Id] uniqueidentifier NOT NULL,
        [WeekStart] date NOT NULL,
        [AvailableMinutes] int NOT NULL,
        [CompletedPlannedMinutes] int NOT NULL,
        [CapacityFactor] float NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_capacity_snapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_capacity_snapshots_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_capacity_snapshots_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [notification_settings] (
        [UserId] uniqueidentifier NOT NULL,
        [InAppEnabled] bit NOT NULL,
        [DeadlineLeadHours] int NOT NULL,
        [SessionReminderLeadMinutes] int NOT NULL,
        [WorkloadWarningLevel] nvarchar(10) NOT NULL,
        CONSTRAINT [PK_notification_settings] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_notification_settings_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [personal_capacity_profiles] (
        [UserId] uniqueidentifier NOT NULL,
        [GlobalEstimationBias] float NOT NULL,
        [CapacityFactor] float NOT NULL,
        [ObservationCount] int NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_personal_capacity_profiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_personal_capacity_profiles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [plan_items] (
        [Id] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [PreviousSessionId] uniqueidentifier NULL,
        [Action] nvarchar(12) NOT NULL,
        [Start] datetimeoffset NULL,
        [End] datetimeoffset NULL,
        [PlannedMinutes] int NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_plan_items] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_plan_items_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_plan_items_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_plan_items_PlanProposals_UserId_PlanId] FOREIGN KEY ([UserId], [PlanId]) REFERENCES [PlanProposals] ([UserId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_plan_items_WorkSessions_UserId_PreviousSessionId] FOREIGN KEY ([UserId], [PreviousSessionId]) REFERENCES [WorkSessions] ([UserId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_plan_items_WorkTasks_UserId_TaskId] FOREIGN KEY ([UserId], [TaskId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [plan_warnings] (
        [Id] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NULL,
        [Code] nvarchar(80) NOT NULL,
        [Detail] nvarchar(1000) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_plan_warnings] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_plan_warnings_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_plan_warnings_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_plan_warnings_PlanProposals_UserId_PlanId] FOREIGN KEY ([UserId], [PlanId]) REFERENCES [PlanProposals] ([UserId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_plan_warnings_WorkTasks_UserId_TaskId] FOREIGN KEY ([UserId], [TaskId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [risk_assessments] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NULL,
        [Scope] nvarchar(10) NOT NULL,
        [Level] nvarchar(10) NOT NULL,
        [Score] float NOT NULL,
        [LoadRatio] float NULL,
        [IsCurrent] bit NOT NULL,
        [ComputedAt] datetimeoffset NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_risk_assessments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_risk_assessments_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_risk_assessments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_risk_assessments_WorkTasks_UserId_TaskId] FOREIGN KEY ([UserId], [TaskId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [session_executions] (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [EndedAt] datetimeoffset NULL,
        [TimerSeconds] bigint NOT NULL,
        [ActualSeconds] bigint NOT NULL,
        [IsUserAdjusted] bit NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_session_executions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_session_executions_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_session_executions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_session_executions_WorkSessions_UserId_SessionId] FOREIGN KEY ([UserId], [SessionId]) REFERENCES [WorkSessions] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [system_job_runs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [JobType] nvarchar(24) NOT NULL,
        [Status] nvarchar(10) NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [FinishedAt] datetimeoffset NULL,
        CONSTRAINT [PK_system_job_runs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_system_job_runs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [task_types] (
        [Code] nvarchar(60) NOT NULL,
        [DisplayName] nvarchar(60) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_task_types] PRIMARY KEY ([Code])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [user_profiles] (
        [UserId] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_user_profiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_user_profiles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [plan_item_reasons] (
        [Id] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Code] nvarchar(80) NOT NULL,
        [DetailJson] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_plan_item_reasons] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_plan_item_reasons_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_plan_item_reasons_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_plan_item_reasons_plan_items_UserId_ItemId] FOREIGN KEY ([UserId], [ItemId]) REFERENCES [plan_items] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [risk_reasons] (
        [Id] uniqueidentifier NOT NULL,
        [AssessmentId] uniqueidentifier NOT NULL,
        [Code] nvarchar(80) NOT NULL,
        [DetailJson] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_risk_reasons] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_risk_reasons_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_risk_reasons_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_risk_reasons_risk_assessments_UserId_AssessmentId] FOREIGN KEY ([UserId], [AssessmentId]) REFERENCES [risk_assessments] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [execution_segments] (
        [Id] uniqueidentifier NOT NULL,
        [ExecutionId] uniqueidentifier NOT NULL,
        [Start] datetimeoffset NOT NULL,
        [End] datetimeoffset NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_execution_segments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_execution_segments_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_execution_segments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_execution_segments_session_executions_UserId_ExecutionId] FOREIGN KEY ([UserId], [ExecutionId]) REFERENCES [session_executions] ([UserId], [Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [estimation_observations] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [TaskType] nvarchar(60) NOT NULL,
        [EstimatedMinutes] int NOT NULL,
        [ActualMinutes] float NOT NULL,
        [ObservedAt] datetimeoffset NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_estimation_observations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_estimation_observations_UserId_Id] UNIQUE ([UserId], [Id]),
        CONSTRAINT [FK_estimation_observations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_estimation_observations_WorkTasks_UserId_TaskId] FOREIGN KEY ([UserId], [TaskId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_estimation_observations_task_types_TaskType] FOREIGN KEY ([TaskType]) REFERENCES [task_types] ([Code]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE TABLE [task_type_biases] (
        [UserId] uniqueidentifier NOT NULL,
        [TaskType] nvarchar(60) NOT NULL,
        [BiasFactor] float NOT NULL,
        [AverageErrorRatio] float NOT NULL,
        [SampleCount] int NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_task_type_biases] PRIMARY KEY ([UserId], [TaskType]),
        CONSTRAINT [FK_task_type_biases_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_task_type_biases_task_types_TaskType] FOREIGN KEY ([TaskType]) REFERENCES [task_types] ([Code]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'DisplayName', N'IsActive') AND [object_id] = OBJECT_ID(N'[task_types]'))
        SET IDENTITY_INSERT [task_types] ON;
    EXEC(N'INSERT INTO [task_types] ([Code], [DisplayName], [IsActive])
    VALUES (N''ADMIN'', N''Admin / chores'', CAST(1 AS bit)),
    (N''MEETING_PREP'', N''Meeting preparation'', CAST(1 AS bit)),
    (N''OTHER'', N''Other'', CAST(1 AS bit)),
    (N''PROGRAMMING'', N''Programming'', CAST(1 AS bit)),
    (N''STUDY'', N''Study'', CAST(1 AS bit)),
    (N''WRITING'', N''Writing'', CAST(1 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'DisplayName', N'IsActive') AND [object_id] = OBJECT_ID(N'[task_types]'))
        SET IDENTITY_INSERT [task_types] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_TaskType] ON [WorkTasks] ([TaskType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkSessions_SourceItemId] ON [WorkSessions] ([SourceItemId]) WHERE [SourceItemId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_WorkSessions_UserId_SourceItemId] ON [WorkSessions] ([UserId], [SourceItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_PlanProposals_UserId_PreviousPlanId] ON [PlanProposals] ([UserId], [PreviousPlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    UPDATE WorkTasks SET TaskType = CASE UPPER(TaskType)
      WHEN 'GENERAL' THEN 'OTHER' WHEN 'LEARNING' THEN 'STUDY' WHEN 'RESEARCH' THEN 'STUDY'
      WHEN 'PERSONAL' THEN 'ADMIN' WHEN 'MEETING' THEN 'MEETING_PREP' ELSE UPPER(TaskType) END,
      CreationSource='MANUAL', IsHardDeadline=1, DeletedAt=CASE WHEN IsDeleted=1 THEN UpdatedAt ELSE NULL END;
    INSERT INTO task_types(Code, DisplayName, IsActive)
      SELECT DISTINCT TaskType, LEFT(TaskType,60), 1 FROM WorkTasks t WHERE NOT EXISTS (SELECT 1 FROM task_types d WHERE d.Code=t.TaskType);
    UPDATE WorkPreferences SET PlanningHorizonDays=14, PreferredPeriod='NONE';
    UPDATE FixedEvents SET EventType='OTHER';
    UPDATE TaskDependencies SET CreatedAt=SYSUTCDATETIME();
    UPDATE WorkSessions SET CreatedAt=Start, UpdatedAt=COALESCE(CompletedAt,Start);
    UPDATE InAppNotifications SET ScheduledFor=CreatedAt, Kind=CASE WHEN Kind='SCHEDULE_CHANGED' THEN 'SCHEDULE_CHANGE' ELSE Kind END;
    INSERT INTO user_profiles(UserId,DisplayName,TimeZoneId,UpdatedAt)
      SELECT u.Id,LEFT(LTRIM(RTRIM(CONCAT(u.FirstName,' ',u.LastName))),100),COALESCE(p.TimeZoneId,'Asia/Ho_Chi_Minh'),u.UpdatedAt
      FROM AspNetUsers u LEFT JOIN WorkPreferences p ON p.UserId=u.Id;
    INSERT INTO notification_settings(UserId,InAppEnabled,DeadlineLeadHours,SessionReminderLeadMinutes,WorkloadWarningLevel)
      SELECT Id,1,24,10,'HIGH' FROM AspNetUsers;
    UPDATE PlanProposals SET Kind='INITIAL', TriggerEvent='MANUAL', Status=CASE WHEN IsApplied=1 THEN 'SUPERSEDED' ELSE 'PROPOSED' END,
      AppliedAt=CASE WHEN IsApplied=1 THEN CreatedAt ELSE NULL END,
      HorizonStart=COALESCE(TRY_CONVERT(datetimeoffset,JSON_VALUE(PayloadJson,'$.Start')),CreatedAt),
      HorizonEnd=COALESCE(TRY_CONVERT(datetimeoffset,JSON_VALUE(PayloadJson,'$.End')),DATEADD(day,7,CreatedAt));
    WITH ranked AS (SELECT Id, ROW_NUMBER() OVER(PARTITION BY UserId ORDER BY CreatedAt DESC, Id DESC) n FROM PlanProposals WHERE IsApplied=1)
      UPDATE p SET Status='APPLIED' FROM PlanProposals p JOIN ranked r ON r.Id=p.Id WHERE r.n=1;
    INSERT INTO plan_items(Id,UserId,PlanId,TaskId,PreviousSessionId,Action,Start,[End],PlannedMinutes)
      SELECT NEWID(),p.UserId,p.Id,j.TaskId,CASE WHEN j.Preserved=1 THEN s.Id ELSE NULL END,
        CASE WHEN j.Preserved=1 THEN 'KEEP' ELSE 'ADD' END,j.Start,j.[End],DATEDIFF(minute,j.Start,j.[End])
      FROM PlanProposals p CROSS APPLY OPENJSON(p.PayloadJson,'$.Sessions') WITH
        (Id uniqueidentifier,TaskId uniqueidentifier,Start datetimeoffset,[End] datetimeoffset,Preserved bit) j
      JOIN WorkTasks t ON t.Id=j.TaskId AND t.UserId=p.UserId LEFT JOIN WorkSessions s ON s.Id=j.Id AND s.UserId=p.UserId;
    INSERT INTO plan_item_reasons(Id,UserId,ItemId,Code,DetailJson)
      SELECT NEWID(),UserId,Id,'LEGACY_PLAN_IMPORT',NULL FROM plan_items;
    INSERT INTO plan_warnings(Id,UserId,PlanId,TaskId,Code,Detail)
      SELECT NEWID(),p.UserId,p.Id,j.TaskId,j.Code,j.Message FROM PlanProposals p
      CROSS APPLY OPENJSON(p.PayloadJson,'$.Warnings') WITH(TaskId uniqueidentifier,Code nvarchar(80),Message nvarchar(1000)) j
      JOIN WorkTasks t ON t.Id=j.TaskId AND t.UserId=p.UserId;
    WITH links AS
      (SELECT s.Id SessionId,chosen.Id ItemId,ROW_NUMBER() OVER(PARTITION BY chosen.Id ORDER BY s.Id) n FROM WorkSessions s CROSS APPLY
        (SELECT TOP(1) i.Id FROM plan_items i JOIN PlanProposals p ON p.Id=i.PlanId
          WHERE i.UserId=s.UserId AND i.TaskId=s.TaskId AND i.Start=s.Start AND i.[End]=s.[End] AND i.Action='ADD' AND p.IsApplied=1 ORDER BY p.CreatedAt) chosen WHERE s.Status<>6)
      UPDATE s SET SourceItemId=l.ItemId FROM WorkSessions s JOIN links l ON l.SessionId=s.Id WHERE l.n=1;
    INSERT INTO session_executions(Id,UserId,SessionId,StartedAt,EndedAt,TimerSeconds,ActualSeconds,IsUserAdjusted)
      SELECT NEWID(),UserId,Id,Start,CASE WHEN Status IN (3,4) THEN COALESCE(CompletedAt,[End]) ELSE NULL END,ActualSeconds,ActualSeconds,0
      FROM WorkSessions WHERE Status IN (1,2,3) OR ActualSeconds>0;
    INSERT INTO execution_segments(Id,UserId,ExecutionId,Start,[End])
      SELECT NEWID(),s.UserId,e.Id,s.SegmentStartedAt,NULL FROM WorkSessions s JOIN session_executions e ON e.SessionId=s.Id WHERE s.SegmentStartedAt IS NOT NULL;
    INSERT INTO estimation_observations(Id,UserId,TaskId,TaskType,EstimatedMinutes,ActualMinutes,ObservedAt)
      SELECT NEWID(),t.UserId,t.Id,t.TaskType,t.EstimatedMinutes,SUM(CONVERT(float,s.ActualSeconds))/60,COALESCE(t.CompletedAt,t.UpdatedAt)
      FROM WorkTasks t JOIN WorkSessions s ON s.TaskId=t.Id AND s.Status=3 WHERE t.Status=2 AND t.EstimatedMinutes>0
        AND NOT EXISTS(SELECT 1 FROM WorkTasks child WHERE child.ParentId=t.Id AND child.IsDeleted=0)
      GROUP BY t.UserId,t.Id,t.TaskType,t.EstimatedMinutes,t.CompletedAt,t.UpdatedAt HAVING SUM(s.ActualSeconds)>0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_InAppNotifications_UserId_PlanId] ON [InAppNotifications] ([UserId], [PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_InAppNotifications_UserId_SessionId] ON [InAppNotifications] ([UserId], [SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_InAppNotifications_UserId_TaskId] ON [InAppNotifications] ([UserId], [TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_account_status_logs_AdminId] ON [account_status_logs] ([AdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_account_status_logs_UserId] ON [account_status_logs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_capacity_snapshots_UserId] ON [capacity_snapshots] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_capacity_snapshots_UserId_WeekStart] ON [capacity_snapshots] ([UserId], [WeekStart]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_estimation_observations_TaskId] ON [estimation_observations] ([TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_estimation_observations_TaskType] ON [estimation_observations] ([TaskType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_estimation_observations_UserId] ON [estimation_observations] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_estimation_observations_UserId_TaskId] ON [estimation_observations] ([UserId], [TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_estimation_observations_UserId_TaskType] ON [estimation_observations] ([UserId], [TaskType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_execution_segments_ExecutionId] ON [execution_segments] ([ExecutionId]) WHERE [End] IS NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_execution_segments_UserId] ON [execution_segments] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_execution_segments_UserId_ExecutionId] ON [execution_segments] ([UserId], [ExecutionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_item_reasons_UserId] ON [plan_item_reasons] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_item_reasons_UserId_ItemId] ON [plan_item_reasons] ([UserId], [ItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_items_UserId] ON [plan_items] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_items_UserId_PlanId] ON [plan_items] ([UserId], [PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_items_UserId_PreviousSessionId] ON [plan_items] ([UserId], [PreviousSessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_items_UserId_TaskId] ON [plan_items] ([UserId], [TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_warnings_UserId] ON [plan_warnings] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_warnings_UserId_PlanId] ON [plan_warnings] ([UserId], [PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_plan_warnings_UserId_TaskId] ON [plan_warnings] ([UserId], [TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_risk_assessments_UserId] ON [risk_assessments] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_risk_assessments_UserId_Scope_TaskId] ON [risk_assessments] ([UserId], [Scope], [TaskId]) WHERE [IsCurrent] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_risk_assessments_UserId_TaskId] ON [risk_assessments] ([UserId], [TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_risk_reasons_UserId] ON [risk_reasons] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_risk_reasons_UserId_AssessmentId] ON [risk_reasons] ([UserId], [AssessmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_session_executions_SessionId] ON [session_executions] ([SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_session_executions_UserId] ON [session_executions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_session_executions_UserId_SessionId] ON [session_executions] ([UserId], [SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_system_job_runs_Status_StartedAt] ON [system_job_runs] ([Status], [StartedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_system_job_runs_UserId] ON [system_job_runs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    CREATE INDEX [IX_task_type_biases_TaskType] ON [task_type_biases] ([TaskType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [InAppNotifications] ADD CONSTRAINT [FK_InAppNotifications_PlanProposals_UserId_PlanId] FOREIGN KEY ([UserId], [PlanId]) REFERENCES [PlanProposals] ([UserId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [InAppNotifications] ADD CONSTRAINT [FK_InAppNotifications_WorkSessions_UserId_SessionId] FOREIGN KEY ([UserId], [SessionId]) REFERENCES [WorkSessions] ([UserId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [InAppNotifications] ADD CONSTRAINT [FK_InAppNotifications_WorkTasks_UserId_TaskId] FOREIGN KEY ([UserId], [TaskId]) REFERENCES [WorkTasks] ([UserId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [PlanProposals] ADD CONSTRAINT [FK_PlanProposals_PlanProposals_UserId_PreviousPlanId] FOREIGN KEY ([UserId], [PreviousPlanId]) REFERENCES [PlanProposals] ([UserId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkSessions] ADD CONSTRAINT [FK_WorkSessions_plan_items_UserId_SourceItemId] FOREIGN KEY ([UserId], [SourceItemId]) REFERENCES [plan_items] ([UserId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    ALTER TABLE [WorkTasks] ADD CONSTRAINT [FK_WorkTasks_task_types_TaskType] FOREIGN KEY ([TaskType]) REFERENCES [task_types] ([Code]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004153305_AlignWithUmlV2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004153305_AlignWithUmlV2', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004154949_AddProfileDetails'
)
BEGIN
    ALTER TABLE [user_profiles] ADD [AvatarUrl] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004154949_AddProfileDetails'
)
BEGIN
    ALTER TABLE [user_profiles] ADD [Bio] nvarchar(1000) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004154949_AddProfileDetails'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004154949_AddProfileDetails', N'8.0.31');
END;
GO

COMMIT;
GO

