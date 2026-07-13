using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StudioTechBI.Infrastructure.Data;

/// <summary>
/// Belt-and-braces table creation for the two hand-authored migrations added without EF CLI
/// tooling (20260712120000_AddReportDesignerConsent, 20260712130000_AddSchemaModelLibrary — see
/// MIGRATIONS.md). In production, `Database.MigrateAsync()` reported success without actually
/// creating these tables ("Migrations applied" logged, then `Invalid object name 'SchemaModels'`
/// on the very next query) — root cause not confirmed, but the leading theory is that runtime
/// migration discovery needs more than just a `[Migration(id)]` attribute on the class without
/// its usual Designer.cs sibling.
///
/// Rather than depend on solving that with no EF CLI available to verify against, this runs the
/// exact same guarded, idempotent DDL directly via ExecuteSqlRawAsync — independent of whether
/// EF ever recognizes the migrations as pending. Safe to call on every startup.
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
