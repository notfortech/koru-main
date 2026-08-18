using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StudioTechBI.Infrastructure.Data;

/// <summary>
/// Guarded, idempotent DDL applied directly via ExecuteSqlRawAsync on every startup —
/// independent of whether EF's migration discovery recognizes any hand-authored migration as
/// pending. This is the real source of truth for schema changes in this environment (see
/// MIGRATIONS.md for why: `Database.MigrateAsync()` reported success in production without
/// actually creating tables from a hand-authored migration, root cause never confirmed since no
/// EF CLI was available to debug it properly).
///
/// Originally covered just ReportDesignerConsents/SchemaModels/SchemaModelFields (Stories 1-2);
/// now also covers the Story 3 AI-Assisted match/consent/publish flow additions, plus the
/// column-alias learning table.
/// </summary>
public static class HandwrittenMigrationsBootstrapper
{
    public static async Task EnsureTablesExistAsync(ApplicationDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        await ExecAsync(context, logger, "ReportDesignerConsents", @"
            IF OBJECT_ID('dbo.ReportDesignerConsents', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportDesignerConsents] (
                    [Id]           UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]     UNIQUEIDENTIFIER NOT NULL,
                    [SchemaHash]   NVARCHAR(128)     NOT NULL,
                    [ApprovedAt]   DATETIME2         NOT NULL,
                    [ApprovedBy]   NVARCHAR(256)     NOT NULL,
                    [CreatedAt]    DATETIME2         NOT NULL,
                    [UpdatedAt]    DATETIME2         NULL,
                    [CreatedBy]    NVARCHAR(MAX)     NULL,
                    [UpdatedBy]    NVARCHAR(MAX)     NULL,
                    [IsDeleted]    BIT               NOT NULL,
                    CONSTRAINT [PK_ReportDesignerConsents] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportDesignerConsents_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE UNIQUE INDEX [IX_ReportDesignerConsents_ClientId_SchemaHash]
                    ON [dbo].[ReportDesignerConsents] ([ClientId], [SchemaHash]);
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "SchemaModels", @"
            IF OBJECT_ID('dbo.SchemaModels', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SchemaModels] (
                    [Id]          UNIQUEIDENTIFIER NOT NULL,
                    [Name]        NVARCHAR(200)     NOT NULL,
                    [Industry]    NVARCHAR(100)     NOT NULL,
                    [Description] NVARCHAR(1000)    NULL,
                    [CreatedAt]   DATETIME2         NOT NULL,
                    [UpdatedAt]   DATETIME2         NULL,
                    [CreatedBy]   NVARCHAR(MAX)     NULL,
                    [UpdatedBy]   NVARCHAR(MAX)     NULL,
                    [IsDeleted]   BIT               NOT NULL,
                    CONSTRAINT [PK_SchemaModels] PRIMARY KEY ([Id])
                );

                CREATE UNIQUE INDEX [IX_SchemaModels_Name] ON [dbo].[SchemaModels] ([Name]);
                CREATE INDEX [IX_SchemaModels_Industry] ON [dbo].[SchemaModels] ([Industry]);
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "SchemaModelFields", @"
            IF OBJECT_ID('dbo.SchemaModelFields', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SchemaModelFields] (
                    [Id]            UNIQUEIDENTIFIER NOT NULL,
                    [SchemaModelId] UNIQUEIDENTIFIER NOT NULL,
                    [FieldName]     NVARCHAR(200)     NOT NULL,
                    [DataType]      NVARCHAR(50)      NOT NULL,
                    [IsRequired]    BIT               NOT NULL,
                    [SortOrder]     INT               NOT NULL,
                    [CreatedAt]     DATETIME2         NOT NULL,
                    [UpdatedAt]     DATETIME2         NULL,
                    [CreatedBy]     NVARCHAR(MAX)     NULL,
                    [UpdatedBy]     NVARCHAR(MAX)     NULL,
                    [IsDeleted]     BIT               NOT NULL,
                    CONSTRAINT [PK_SchemaModelFields] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SchemaModelFields_SchemaModels_SchemaModelId]
                        FOREIGN KEY ([SchemaModelId]) REFERENCES [dbo].[SchemaModels] ([Id])
                        ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_SchemaModelFields_SchemaModelId_FieldName]
                    ON [dbo].[SchemaModelFields] ([SchemaModelId], [FieldName]);
            END
        ", cancellationToken);

        // CLEANUP: an earlier version of this bootstrapper incorrectly added a second FK on
        // Templates.ModelId, not knowing production already has an unrelated dbo.Models table
        // with its own FK_Templates_Models on that same column. Every Template insert then had
        // to satisfy both simultaneously, which is impossible for rows pointing at SchemaModels.
        // Drop the mistaken FK if it's still there — safe no-op once cleaned up everywhere.
        await ExecAsync(context, logger, "Templates.ModelId FK (cleanup)", @"
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Templates_SchemaModels_ModelId')
                ALTER TABLE [dbo].[Templates] DROP CONSTRAINT [FK_Templates_SchemaModels_ModelId];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Templates_ModelId' AND object_id = OBJECT_ID('dbo.Templates'))
                DROP INDEX [IX_Templates_ModelId] ON [dbo].[Templates];
        ", cancellationToken);

        // Templates.ModelId is left alone entirely — it belongs to that pre-existing dbo.Models
        // table. SchemaModel linkage gets its own column instead.
        await ExecAsync(context, logger, "Templates.SchemaModelId column + FK", @"
            IF COL_LENGTH('dbo.Templates', 'SchemaModelId') IS NULL
                ALTER TABLE [dbo].[Templates] ADD [SchemaModelId] UNIQUEIDENTIFIER NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Templates_SchemaModelId')
                CREATE INDEX [IX_Templates_SchemaModelId] ON [dbo].[Templates] ([SchemaModelId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = 'FK_Templates_SchemaModels_SchemaModelId'
            )
            BEGIN
                ALTER TABLE [dbo].[Templates] WITH NOCHECK
                    ADD CONSTRAINT [FK_Templates_SchemaModels_SchemaModelId]
                    FOREIGN KEY ([SchemaModelId]) REFERENCES [dbo].[SchemaModels] ([Id])
                    ON DELETE SET NULL;
            END
        ", cancellationToken);

        // ── Story 3: AI-Assisted match/consent/publish flow ────────────────────────────────

        await ExecAsync(context, logger, "SchemaModels review columns", @"
            IF COL_LENGTH('dbo.SchemaModels', 'IsAiSuggested') IS NULL
                ALTER TABLE [dbo].[SchemaModels] ADD [IsAiSuggested] BIT NOT NULL
                    CONSTRAINT [DF_SchemaModels_IsAiSuggested] DEFAULT (0);

            IF COL_LENGTH('dbo.SchemaModels', 'ReviewStatus') IS NULL
                ALTER TABLE [dbo].[SchemaModels] ADD [ReviewStatus] NVARCHAR(20) NOT NULL
                    CONSTRAINT [DF_SchemaModels_ReviewStatus] DEFAULT ('Approved');

            IF COL_LENGTH('dbo.SchemaModels', 'SuggestedByClientId') IS NULL
                ALTER TABLE [dbo].[SchemaModels] ADD [SuggestedByClientId] UNIQUEIDENTIFIER NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SchemaModels_ReviewStatus')
                CREATE INDEX [IX_SchemaModels_ReviewStatus] ON [dbo].[SchemaModels] ([ReviewStatus]);
        ", cancellationToken);

        await ExecAsync(context, logger, "SchemaModels.SuggestedByClientId FK", @"
            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = 'FK_SchemaModels_Clients_SuggestedByClientId'
            )
            BEGIN
                ALTER TABLE [dbo].[SchemaModels] WITH NOCHECK
                    ADD CONSTRAINT [FK_SchemaModels_Clients_SuggestedByClientId]
                    FOREIGN KEY ([SuggestedByClientId]) REFERENCES [dbo].[Clients] ([Id])
                    ON DELETE SET NULL;
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "Templates.IsPublishReady column", @"
            IF COL_LENGTH('dbo.Templates', 'IsPublishReady') IS NULL
                ALTER TABLE [dbo].[Templates] ADD [IsPublishReady] BIT NOT NULL
                    CONSTRAINT [DF_Templates_IsPublishReady] DEFAULT (0);
        ", cancellationToken);

        // ── Client white-labeling ────────────────────────────────────────────────────────────
        await ExecAsync(context, logger, "Clients.LogoBlobPath column", @"
            IF COL_LENGTH('dbo.Clients', 'LogoBlobPath') IS NULL
                ALTER TABLE [dbo].[Clients] ADD [LogoBlobPath] NVARCHAR(500) NULL;
        ", cancellationToken);

        await ExecAsync(context, logger, "Clients.IsPremiumSubscriber column", @"
            IF COL_LENGTH('dbo.Clients', 'IsPremiumSubscriber') IS NULL
                ALTER TABLE [dbo].[Clients] ADD [IsPremiumSubscriber] BIT NOT NULL
                    CONSTRAINT [DF_Clients_IsPremiumSubscriber] DEFAULT (0);
        ", cancellationToken);

        await ExecAsync(context, logger, "Clients.HasReportValidationAddOn column", @"
            IF COL_LENGTH('dbo.Clients', 'HasReportValidationAddOn') IS NULL
                ALTER TABLE [dbo].[Clients] ADD [HasReportValidationAddOn] BIT NOT NULL
                    CONSTRAINT [DF_Clients_HasReportValidationAddOn] DEFAULT (0);
        ", cancellationToken);

        await ExecAsync(context, logger, "Clients.HasLimitedPortalAccess column", @"
            IF COL_LENGTH('dbo.Clients', 'HasLimitedPortalAccess') IS NULL
                ALTER TABLE [dbo].[Clients] ADD [HasLimitedPortalAccess] BIT NOT NULL
                    CONSTRAINT [DF_Clients_HasLimitedPortalAccess] DEFAULT (0);
        ", cancellationToken);

        await ExecAsync(context, logger, "ReportMatchDrafts", @"
            IF OBJECT_ID('dbo.ReportMatchDrafts', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportMatchDrafts] (
                    [Id]                     UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]               UNIQUEIDENTIFIER NOT NULL,
                    [SchemaModelId]          UNIQUEIDENTIFIER NULL,
                    [TemplateId]             UNIQUEIDENTIFIER NULL,
                    [SchemaHash]             NVARCHAR(128)     NOT NULL,
                    [Status]                 NVARCHAR(20)      NOT NULL,
                    [PublishedAt]            DATETIME2         NULL,
                    [DataRetentionExpiresAt] DATETIME2         NULL,
                    [CreatedAt]              DATETIME2         NOT NULL,
                    [UpdatedAt]              DATETIME2         NULL,
                    [CreatedBy]              NVARCHAR(MAX)     NULL,
                    [UpdatedBy]              NVARCHAR(MAX)     NULL,
                    [IsDeleted]              BIT               NOT NULL,
                    CONSTRAINT [PK_ReportMatchDrafts] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportMatchDrafts_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION,
                    CONSTRAINT [FK_ReportMatchDrafts_SchemaModels_SchemaModelId]
                        FOREIGN KEY ([SchemaModelId]) REFERENCES [dbo].[SchemaModels] ([Id])
                        ON DELETE SET NULL,
                    CONSTRAINT [FK_ReportMatchDrafts_Templates_TemplateId]
                        FOREIGN KEY ([TemplateId]) REFERENCES [dbo].[Templates] ([Id])
                        ON DELETE SET NULL
                );

                CREATE INDEX [IX_ReportMatchDrafts_ClientId] ON [dbo].[ReportMatchDrafts] ([ClientId]);
                CREATE INDEX [IX_ReportMatchDrafts_Status] ON [dbo].[ReportMatchDrafts] ([Status]);
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "ReportMatchColumnMappings", @"
            IF OBJECT_ID('dbo.ReportMatchColumnMappings', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportMatchColumnMappings] (
                    [Id]                 UNIQUEIDENTIFIER NOT NULL,
                    [ReportMatchDraftId] UNIQUEIDENTIFIER NOT NULL,
                    [FieldName]          NVARCHAR(200)     NOT NULL,
                    [DataType]           NVARCHAR(50)      NOT NULL,
                    [ClientColumnName]   NVARCHAR(200)     NULL,
                    [Included]           BIT               NOT NULL,
                    [CreatedAt]          DATETIME2         NOT NULL,
                    [UpdatedAt]          DATETIME2         NULL,
                    [CreatedBy]          NVARCHAR(MAX)     NULL,
                    [UpdatedBy]          NVARCHAR(MAX)     NULL,
                    [IsDeleted]          BIT               NOT NULL,
                    CONSTRAINT [PK_ReportMatchColumnMappings] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportMatchColumnMappings_ReportMatchDrafts_ReportMatchDraftId]
                        FOREIGN KEY ([ReportMatchDraftId]) REFERENCES [dbo].[ReportMatchDrafts] ([Id])
                        ON DELETE CASCADE
                );

                CREATE INDEX [IX_ReportMatchColumnMappings_ReportMatchDraftId]
                    ON [dbo].[ReportMatchColumnMappings] ([ReportMatchDraftId]);
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "ReportDataUsageConsents", @"
            IF OBJECT_ID('dbo.ReportDataUsageConsents', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportDataUsageConsents] (
                    [Id]                 UNIQUEIDENTIFIER NOT NULL,
                    [ReportMatchDraftId] UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]           UNIQUEIDENTIFIER NOT NULL,
                    [ApprovedAt]         DATETIME2         NOT NULL,
                    [ApprovedBy]         NVARCHAR(256)     NOT NULL,
                    [CreatedAt]          DATETIME2         NOT NULL,
                    [UpdatedAt]          DATETIME2         NULL,
                    [CreatedBy]          NVARCHAR(MAX)     NULL,
                    [UpdatedBy]          NVARCHAR(MAX)     NULL,
                    [IsDeleted]          BIT               NOT NULL,
                    CONSTRAINT [PK_ReportDataUsageConsents] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportDataUsageConsents_ReportMatchDrafts_ReportMatchDraftId]
                        FOREIGN KEY ([ReportMatchDraftId]) REFERENCES [dbo].[ReportMatchDrafts] ([Id])
                        ON DELETE CASCADE
                );

                -- Deliberately no unique index — append-only, one row per Publish action.
                CREATE INDEX [IX_ReportDataUsageConsents_ReportMatchDraftId]
                    ON [dbo].[ReportDataUsageConsents] ([ReportMatchDraftId]);
                CREATE INDEX [IX_ReportDataUsageConsents_ClientId]
                    ON [dbo].[ReportDataUsageConsents] ([ClientId]);
            END
        ", cancellationToken);

        // ── Column-alias learning ───────────────────────────────────────────────────────────

        await ExecAsync(context, logger, "SchemaModelFieldAliases", @"
            IF OBJECT_ID('dbo.SchemaModelFieldAliases', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SchemaModelFieldAliases] (
                    [Id]                          UNIQUEIDENTIFIER NOT NULL,
                    [SchemaModelFieldId]          UNIQUEIDENTIFIER NOT NULL,
                    [AliasName]                   NVARCHAR(200)     NOT NULL,
                    [NormalizedAliasName]         NVARCHAR(200)     NOT NULL,
                    [ObservedDataType]            NVARCHAR(50)      NULL,
                    [Confidence]                  FLOAT             NOT NULL,
                    [Source]                      NVARCHAR(20)      NOT NULL,
                    [ApprovalStatus]              NVARCHAR(20)      NOT NULL,
                    [ObservedCount]                INT              NOT NULL,
                    [FirstSeenAt]                 DATETIME2         NOT NULL,
                    [LastSeenAt]                  DATETIME2         NOT NULL,
                    [FirstSeenClientId]           UNIQUEIDENTIFIER  NULL,
                    [FirstSeenReportMatchDraftId] UNIQUEIDENTIFIER  NULL,
                    [DecidedBy]                   NVARCHAR(256)     NULL,
                    [DecidedAt]                   DATETIME2         NULL,
                    [CreatedAt]                   DATETIME2         NOT NULL,
                    [UpdatedAt]                   DATETIME2         NULL,
                    [CreatedBy]                   NVARCHAR(MAX)     NULL,
                    [UpdatedBy]                   NVARCHAR(MAX)     NULL,
                    [IsDeleted]                   BIT               NOT NULL,
                    CONSTRAINT [PK_SchemaModelFieldAliases] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SchemaModelFieldAliases_SchemaModelFields_SchemaModelFieldId]
                        FOREIGN KEY ([SchemaModelFieldId]) REFERENCES [dbo].[SchemaModelFields] ([Id])
                        ON DELETE CASCADE,
                    CONSTRAINT [FK_SchemaModelFieldAliases_ReportMatchDrafts_FirstSeenReportMatchDraftId]
                        FOREIGN KEY ([FirstSeenReportMatchDraftId]) REFERENCES [dbo].[ReportMatchDrafts] ([Id])
                        ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX [IX_SchemaModelFieldAliases_FieldId_NormalizedAliasName]
                    ON [dbo].[SchemaModelFieldAliases] ([SchemaModelFieldId], [NormalizedAliasName]);
                CREATE INDEX [IX_SchemaModelFieldAliases_NormalizedAliasName_ApprovalStatus]
                    ON [dbo].[SchemaModelFieldAliases] ([NormalizedAliasName], [ApprovalStatus]);
                CREATE INDEX [IX_SchemaModelFieldAliases_ApprovalStatus]
                    ON [dbo].[SchemaModelFieldAliases] ([ApprovalStatus]);
            END
        ", cancellationToken);

        // ── AI-boundary audit log ───────────────────────────────────────────────────────────

        await ExecAsync(context, logger, "AiBoundaryAuditEvents", @"
            IF OBJECT_ID('dbo.AiBoundaryAuditEvents', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AiBoundaryAuditEvents] (
                    [Id]             UNIQUEIDENTIFIER NOT NULL,
                    [CorrelationId]  NVARCHAR(100)     NOT NULL,
                    [Service]        NVARCHAR(100)     NOT NULL,
                    [Operation]      NVARCHAR(100)     NOT NULL,
                    [Phase]          NVARCHAR(20)      NOT NULL,
                    [TargetService]  NVARCHAR(100)     NOT NULL,
                    [MetadataJson]   NVARCHAR(MAX)     NOT NULL,
                    [DurationMs]     BIGINT            NULL,
                    [StatusCode]     INT               NULL,
                    [Success]        BIT               NULL,
                    [ErrorSummary]   NVARCHAR(500)     NULL,
                    [ClientId]       UNIQUEIDENTIFIER  NULL,
                    [CreatedAt]      DATETIME2         NOT NULL,
                    [UpdatedAt]      DATETIME2         NULL,
                    [CreatedBy]      NVARCHAR(MAX)     NULL,
                    [UpdatedBy]      NVARCHAR(MAX)     NULL,
                    [IsDeleted]      BIT               NOT NULL,
                    CONSTRAINT [PK_AiBoundaryAuditEvents] PRIMARY KEY ([Id])
                );

                CREATE INDEX [IX_AiBoundaryAuditEvents_CorrelationId]
                    ON [dbo].[AiBoundaryAuditEvents] ([CorrelationId]);
                CREATE INDEX [IX_AiBoundaryAuditEvents_Operation_CreatedAt]
                    ON [dbo].[AiBoundaryAuditEvents] ([Operation], [CreatedAt]);
                CREATE INDEX [IX_AiBoundaryAuditEvents_Success]
                    ON [dbo].[AiBoundaryAuditEvents] ([Success]);
            END
        ", cancellationToken);

        // ── Report Validation (Phase 1: Rendering Health + Data Sanity) ─────────────────────────
        await ExecAsync(context, logger, "ReportValidationRuns", @"
            IF OBJECT_ID('dbo.ReportValidationRuns', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportValidationRuns] (
                    [Id]                         UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]                   UNIQUEIDENTIFIER NOT NULL,
                    [RequestedByUserId]          UNIQUEIDENTIFIER NOT NULL,
                    [Status]                     NVARCHAR(20)      NOT NULL,
                    [OverallResult]               NVARCHAR(20)      NULL,
                    [TemplateId]                 NVARCHAR(200)     NULL,
                    [TemplateName]               NVARCHAR(200)     NULL,
                    [FiltersJson]                NVARCHAR(MAX)     NULL,
                    [ReportSnapshotJson]         NVARCHAR(MAX)     NOT NULL,
                    [SourceFileScratchBlobPath]  NVARCHAR(500)     NULL,
                    [ProcessingStartedAt]        DATETIME2         NULL,
                    [CompletedAt]                DATETIME2         NULL,
                    [ErrorMessage]               NVARCHAR(MAX)     NULL,
                    [CreatedAt]                  DATETIME2         NOT NULL,
                    [UpdatedAt]                  DATETIME2         NULL,
                    [CreatedBy]                  NVARCHAR(MAX)     NULL,
                    [UpdatedBy]                  NVARCHAR(MAX)     NULL,
                    [IsDeleted]                  BIT               NOT NULL,
                    CONSTRAINT [PK_ReportValidationRuns] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportValidationRuns_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_ReportValidationRuns_ClientId] ON [dbo].[ReportValidationRuns] ([ClientId]);
                CREATE INDEX [IX_ReportValidationRuns_Status] ON [dbo].[ReportValidationRuns] ([Status]);
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "ReportValidationChecks", @"
            IF OBJECT_ID('dbo.ReportValidationChecks', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportValidationChecks] (
                    [Id]                     UNIQUEIDENTIFIER NOT NULL,
                    [ReportValidationRunId]  UNIQUEIDENTIFIER NOT NULL,
                    [CheckFamily]            NVARCHAR(30)      NOT NULL,
                    [CheckName]              NVARCHAR(100)     NOT NULL,
                    [Status]                 NVARCHAR(20)      NOT NULL,
                    [Detail]                 NVARCHAR(2000)    NULL,
                    [EvidenceJson]           NVARCHAR(MAX)     NULL,
                    [SortOrder]              INT               NOT NULL,
                    [CreatedAt]              DATETIME2         NOT NULL,
                    [UpdatedAt]              DATETIME2         NULL,
                    [CreatedBy]              NVARCHAR(MAX)     NULL,
                    [UpdatedBy]              NVARCHAR(MAX)     NULL,
                    [IsDeleted]              BIT               NOT NULL,
                    CONSTRAINT [PK_ReportValidationChecks] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportValidationChecks_ReportValidationRuns_ReportValidationRunId]
                        FOREIGN KEY ([ReportValidationRunId]) REFERENCES [dbo].[ReportValidationRuns] ([Id])
                        ON DELETE CASCADE
                );

                CREATE INDEX [IX_ReportValidationChecks_ReportValidationRunId]
                    ON [dbo].[ReportValidationChecks] ([ReportValidationRunId]);
            END
        ", cancellationToken);

        // ── Saved Reports (self-serve HTML reports + fulfilled custom Power BI requests) ───────
        await ExecAsync(context, logger, "SavedReports", @"
            IF OBJECT_ID('dbo.SavedReports', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SavedReports] (
                    [Id]              UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]        UNIQUEIDENTIFIER NOT NULL,
                    [Title]           NVARCHAR(300)     NOT NULL,
                    [SourceType]      NVARCHAR(50)      NOT NULL,
                    [Status]          NVARCHAR(50)      NOT NULL,
                    [VersionCount]    INT               NOT NULL,
                    [PowerBiAssetId]  UNIQUEIDENTIFIER  NULL,
                    [CreatedAt]       DATETIME2         NOT NULL,
                    [UpdatedAt]       DATETIME2         NULL,
                    [CreatedBy]       NVARCHAR(500)     NULL,
                    [UpdatedBy]       NVARCHAR(500)     NULL,
                    [IsDeleted]       BIT               NOT NULL,
                    CONSTRAINT [PK_SavedReports] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SavedReports_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_SavedReports_ClientId_Status] ON [dbo].[SavedReports] ([ClientId], [Status]);
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "SavedReportVersions", @"
            IF OBJECT_ID('dbo.SavedReportVersions', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SavedReportVersions] (
                    [Id]                  UNIQUEIDENTIFIER NOT NULL,
                    [SavedReportId]       UNIQUEIDENTIFIER NOT NULL,
                    [VersionNumber]       INT               NOT NULL,
                    [HtmlBlobPath]        NVARCHAR(1000)    NULL,
                    [TemplateId]          NVARCHAR(200)     NULL,
                    [TemplateName]        NVARCHAR(200)     NULL,
                    [HtmlTemplateId]      NVARCHAR(200)     NULL,
                    [HtmlTemplateName]    NVARCHAR(200)     NULL,
                    [SourceFileName]      NVARCHAR(500)     NULL,
                    [AppliedFiltersJson]  NVARCHAR(MAX)     NULL,
                    [GeneratedDate]       DATETIME2         NOT NULL,
                    [IsActive]            BIT               NOT NULL,
                    [CreatedAt]           DATETIME2         NOT NULL,
                    [UpdatedAt]           DATETIME2         NULL,
                    [CreatedBy]           NVARCHAR(500)     NULL,
                    [UpdatedBy]           NVARCHAR(500)     NULL,
                    [IsDeleted]           BIT               NOT NULL,
                    CONSTRAINT [PK_SavedReportVersions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SavedReportVersions_SavedReports_SavedReportId]
                        FOREIGN KEY ([SavedReportId]) REFERENCES [dbo].[SavedReports] ([Id])
                        ON DELETE CASCADE
                );

                CREATE INDEX [IX_SavedReportVersions_SavedReportId_IsActive]
                    ON [dbo].[SavedReportVersions] ([SavedReportId], [IsActive]);
            END
        ", cancellationToken);

        // ── Custom Power BI report requests (analyst-fulfilled, mostly manual) ──────────────────
        await ExecAsync(context, logger, "CustomReportRequests", @"
            IF OBJECT_ID('dbo.CustomReportRequests', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CustomReportRequests] (
                    [Id]                     UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]               UNIQUEIDENTIFIER NOT NULL,
                    [Status]                 NVARCHAR(50)      NOT NULL,
                    [RequestedByEmail]       NVARCHAR(320)     NULL,
                    [Notes]                  NVARCHAR(2000)    NULL,
                    [SchemaSnapshotJson]     NVARCHAR(MAX)     NOT NULL,
                    [SourceFileName]         NVARCHAR(500)     NULL,
                    [FulfilledSavedReportId] UNIQUEIDENTIFIER  NULL,
                    [FulfilledAtUtc]         DATETIME2         NULL,
                    [FulfilledByEmail]       NVARCHAR(320)     NULL,
                    [CreatedAt]              DATETIME2         NOT NULL,
                    [UpdatedAt]              DATETIME2         NULL,
                    [CreatedBy]              NVARCHAR(500)     NULL,
                    [UpdatedBy]              NVARCHAR(500)     NULL,
                    [IsDeleted]              BIT               NOT NULL,
                    CONSTRAINT [PK_CustomReportRequests] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CustomReportRequests_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_CustomReportRequests_ClientId_Status]
                    ON [dbo].[CustomReportRequests] ([ClientId], [Status]);
            END
        ", cancellationToken);

        await ExecAsync(context, logger, "CustomReportRequests.BlobPath/ExportedToBlobAtUtc columns", @"
            IF COL_LENGTH('dbo.CustomReportRequests', 'BlobPath') IS NULL
                ALTER TABLE [dbo].[CustomReportRequests] ADD [BlobPath] NVARCHAR(1000) NULL;

            IF COL_LENGTH('dbo.CustomReportRequests', 'ExportedToBlobAtUtc') IS NULL
                ALTER TABLE [dbo].[CustomReportRequests] ADD [ExportedToBlobAtUtc] DATETIME2 NULL;
        ", cancellationToken);

        // Distinguishes "we searched and found nothing confident" from "the AI/network call
        // itself failed" so staff can triage the right way (build a template vs. check for an
        // outage) without opening every ticket -- see CustomReportRequestReasons.
        // Each statement is its own ExecAsync/batch -- SQL Server compiles a batch against the
        // schema snapshot taken before it runs, so ADD-then-UPDATE/ALTER-COLUMN in the same batch
        // fails with "Invalid column name" (Error 207) even though the ADD itself is valid.
        await ExecAsync(context, logger, "CustomReportRequests.RequestReason column (add)", @"
            IF COL_LENGTH('dbo.CustomReportRequests', 'RequestReason') IS NULL
                ALTER TABLE [dbo].[CustomReportRequests] ADD [RequestReason] NVARCHAR(50) NULL;
        ", cancellationToken);

        await ExecAsync(context, logger, "CustomReportRequests.RequestReason column (backfill)", @"
            UPDATE [dbo].[CustomReportRequests] SET [RequestReason] = 'NoConfidentMatch' WHERE [RequestReason] IS NULL;
        ", cancellationToken);

        await ExecAsync(context, logger, "CustomReportRequests.RequestReason column (not null)", @"
            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('dbo.CustomReportRequests') AND name = 'RequestReason' AND is_nullable = 1
            )
                ALTER TABLE [dbo].[CustomReportRequests] ALTER COLUMN [RequestReason] NVARCHAR(50) NOT NULL;
        ", cancellationToken);

        // A not-yet-a-client user (e.g. self-registered, limited-access) can now file a custom
        // report request too -- the ticket and schema snapshot still get captured for staff, an
        // admin fills in ClientId later by assigning the requester to a Client before fulfilling.
        // FK_CustomReportRequests_Clients_ClientId still applies whenever a value IS present.
        await ExecAsync(context, logger, "CustomReportRequests.ClientId column (nullable)", @"
            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('dbo.CustomReportRequests') AND name = 'ClientId' AND is_nullable = 0
            )
                ALTER TABLE [dbo].[CustomReportRequests] ALTER COLUMN [ClientId] UNIQUEIDENTIFIER NULL;
        ", cancellationToken);

        // ── Report generation events (Report Stats: deterministic vs. AI-assisted counts) ───────
        await ExecAsync(context, logger, "ReportGenerationEvents", @"
            IF OBJECT_ID('dbo.ReportGenerationEvents', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportGenerationEvents] (
                    [Id]                UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]          UNIQUEIDENTIFIER NOT NULL,
                    [Mode]              NVARCHAR(20)      NOT NULL,
                    [TemplateId]        NVARCHAR(200)     NULL,
                    [TemplateName]      NVARCHAR(200)     NULL,
                    [HtmlTemplateId]    NVARCHAR(200)     NULL,
                    [HtmlTemplateName]  NVARCHAR(200)     NULL,
                    [CreatedAt]         DATETIME2         NOT NULL,
                    [UpdatedAt]         DATETIME2         NULL,
                    [CreatedBy]         NVARCHAR(500)     NULL,
                    [UpdatedBy]         NVARCHAR(500)     NULL,
                    [IsDeleted]         BIT               NOT NULL,
                    CONSTRAINT [PK_ReportGenerationEvents] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportGenerationEvents_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_ReportGenerationEvents_ClientId_Mode]
                    ON [dbo].[ReportGenerationEvents] ([ClientId], [Mode]);
            END
        ", cancellationToken);

        // ── AI credit purchase requests (mock checkout, admin-fulfilled) ────────────────────────
        await ExecAsync(context, logger, "CreditPurchaseRequests", @"
            IF OBJECT_ID('dbo.CreditPurchaseRequests', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CreditPurchaseRequests] (
                    [Id]                 UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]           UNIQUEIDENTIFIER NOT NULL,
                    [RequestedByEmail]   NVARCHAR(320)     NULL,
                    [CreditsRequested]   INT               NOT NULL,
                    [PackLabel]          NVARCHAR(100)     NOT NULL,
                    [Status]             NVARCHAR(50)      NOT NULL,
                    [Notes]              NVARCHAR(2000)    NULL,
                    [PaidAtUtc]          DATETIME2         NULL,
                    [PaidByEmail]        NVARCHAR(320)     NULL,
                    [CreatedAt]          DATETIME2         NOT NULL,
                    [UpdatedAt]          DATETIME2         NULL,
                    [CreatedBy]          NVARCHAR(500)     NULL,
                    [UpdatedBy]          NVARCHAR(500)     NULL,
                    [IsDeleted]          BIT               NOT NULL,
                    CONSTRAINT [PK_CreditPurchaseRequests] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CreditPurchaseRequests_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_CreditPurchaseRequests_ClientId_Status]
                    ON [dbo].[CreditPurchaseRequests] ([ClientId], [Status]);
            END
        ", cancellationToken);

        // ── Local interim AI credit ledger (see LocalCreditLedgerService) ───────────────────────
        await ExecAsync(context, logger, "Clients.AiCreditsRemaining column", @"
            IF COL_LENGTH('dbo.Clients', 'AiCreditsRemaining') IS NULL
                ALTER TABLE [dbo].[Clients] ADD [AiCreditsRemaining] INT NOT NULL
                    CONSTRAINT [DF_Clients_AiCreditsRemaining] DEFAULT (1000);
        ", cancellationToken);

        await ExecAsync(context, logger, "CreditPurchaseRequests.Source column", @"
            IF COL_LENGTH('dbo.CreditPurchaseRequests', 'Source') IS NULL
                ALTER TABLE [dbo].[CreditPurchaseRequests] ADD [Source] NVARCHAR(20) NOT NULL
                    CONSTRAINT [DF_CreditPurchaseRequests_Source] DEFAULT ('Client');
        ", cancellationToken);

        // ── Large-file Report Generator uploads (direct-to-blob + durable async processing) ─────
        await ExecAsync(context, logger, "ReportGenerationJobs", @"
            IF OBJECT_ID('dbo.ReportGenerationJobs', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportGenerationJobs] (
                    [Id]                   UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]             UNIQUEIDENTIFIER NOT NULL,
                    [BlobPath]             NVARCHAR(1000)    NOT NULL,
                    [FileName]             NVARCHAR(500)     NOT NULL,
                    [Status]               NVARCHAR(20)      NOT NULL,
                    [RequestPayloadJson]   NVARCHAR(MAX)     NULL,
                    [ResultJson]           NVARCHAR(MAX)     NULL,
                    [ErrorMessage]         NVARCHAR(MAX)     NULL,
                    [ProcessingStartedAt]  DATETIME2         NULL,
                    [CompletedAt]          DATETIME2         NULL,
                    [CorrelationId]        NVARCHAR(100)     NULL,
                    [CreatedAt]            DATETIME2         NOT NULL,
                    [UpdatedAt]            DATETIME2         NULL,
                    [CreatedBy]            NVARCHAR(500)     NULL,
                    [UpdatedBy]            NVARCHAR(500)     NULL,
                    [IsDeleted]            BIT               NOT NULL,
                    CONSTRAINT [PK_ReportGenerationJobs] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportGenerationJobs_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_ReportGenerationJobs_ClientId] ON [dbo].[ReportGenerationJobs] ([ClientId]);
                CREATE INDEX [IX_ReportGenerationJobs_Status] ON [dbo].[ReportGenerationJobs] ([Status]);
            END
        ", cancellationToken);

        // ── Async AI-assisted Report Generator "Data Model" generation ─────────────────────────
        // Lets the client navigate away from the wizard mid-generation and come back later instead
        // of holding the browser connection open for the multi-minute LLM call -- mirrors
        // BlueprintGenerations' shape/lifecycle exactly.
        await ExecAsync(context, logger, "ReportModelGenerations", @"
            IF OBJECT_ID('dbo.ReportModelGenerations', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ReportModelGenerations] (
                    [Id]                   UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]             UNIQUEIDENTIFIER NOT NULL,
                    [RequestId]            NVARCHAR(100)     NOT NULL,
                    [Status]               NVARCHAR(20)      NOT NULL,
                    [RequestPayloadJson]   NVARCHAR(MAX)     NOT NULL,
                    [ResponseJson]         NVARCHAR(MAX)     NULL,
                    [ErrorMessage]         NVARCHAR(MAX)     NULL,
                    [ProcessingStartedAt]  DATETIME2         NULL,
                    [CompletedAt]          DATETIME2         NULL,
                    [CreatedAt]            DATETIME2         NOT NULL,
                    [UpdatedAt]            DATETIME2         NULL,
                    [CreatedBy]            NVARCHAR(500)     NULL,
                    [UpdatedBy]            NVARCHAR(500)     NULL,
                    [IsDeleted]            BIT               NOT NULL,
                    CONSTRAINT [PK_ReportModelGenerations] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ReportModelGenerations_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_ReportModelGenerations_ClientId] ON [dbo].[ReportModelGenerations] ([ClientId]);
                CREATE INDEX [IX_ReportModelGenerations_Status] ON [dbo].[ReportModelGenerations] ([Status]);
            END
        ", cancellationToken);

        // ── Async AI-assisted Report Generator schema-model library match ─────────────────────
        // Lets the client navigate away from the wizard mid-match and come back later instead of
        // holding the browser connection open for up to ~330s (an AI-escalated match can take the
        // full outbound AI budget) -- mirrors ReportModelGenerations' shape/lifecycle exactly.
        await ExecAsync(context, logger, "SchemaModelMatches", @"
            IF OBJECT_ID('dbo.SchemaModelMatches', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SchemaModelMatches] (
                    [Id]                   UNIQUEIDENTIFIER NOT NULL,
                    [ClientId]             UNIQUEIDENTIFIER NOT NULL,
                    [RequestId]            NVARCHAR(100)     NOT NULL,
                    [Status]               NVARCHAR(20)      NOT NULL,
                    [RequestPayloadJson]   NVARCHAR(MAX)     NOT NULL,
                    [ResponseJson]         NVARCHAR(MAX)     NULL,
                    [ErrorMessage]         NVARCHAR(MAX)     NULL,
                    [ProcessingStartedAt]  DATETIME2         NULL,
                    [CompletedAt]          DATETIME2         NULL,
                    [CreatedAt]            DATETIME2         NOT NULL,
                    [UpdatedAt]            DATETIME2         NULL,
                    [CreatedBy]            NVARCHAR(500)     NULL,
                    [UpdatedBy]            NVARCHAR(500)     NULL,
                    [IsDeleted]            BIT               NOT NULL,
                    CONSTRAINT [PK_SchemaModelMatches] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SchemaModelMatches_Clients_ClientId]
                        FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                        ON DELETE NO ACTION
                );

                CREATE INDEX [IX_SchemaModelMatches_ClientId] ON [dbo].[SchemaModelMatches] ([ClientId]);
                CREATE INDEX [IX_SchemaModelMatches_Status] ON [dbo].[SchemaModelMatches] ([Status]);
            END
        ", cancellationToken);

        // Sign-up data/terms disclaimer acceptance -- both nullable since existing accounts
        // predate this and are never retroactively required to consent.
        await ExecAsync(context, logger, "Users.TermsAcceptedAt/TermsVersion columns", @"
            IF COL_LENGTH('dbo.Users', 'TermsAcceptedAt') IS NULL
                ALTER TABLE [dbo].[Users] ADD [TermsAcceptedAt] DATETIME2 NULL;

            IF COL_LENGTH('dbo.Users', 'TermsVersion') IS NULL
                ALTER TABLE [dbo].[Users] ADD [TermsVersion] NVARCHAR(20) NULL;
        ", cancellationToken);
    }

    private static async Task ExecAsync(ApplicationDbContext context, ILogger logger, string label, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-fatal by design — this is a defensive fallback. If it fails, the seeder
            // that runs after it will surface a clearer error naming the missing table.
            logger.LogWarning(ex, "HandwrittenMigrationsBootstrapper: failed ensuring '{Label}'.", label);
        }
    }
}
