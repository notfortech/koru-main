using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored, kept for history/future `dotnet ef migrations add` reconciliation only —
    // HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync is the actual source of truth for
    // whether this schema exists (see MIGRATIONS.md for why hand-authored migrations alone were
    // not reliable in this environment).
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260713000000_AddReportMatchDraftFlow")]
    public partial class AddReportMatchDraftFlow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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

                IF COL_LENGTH('dbo.Templates', 'IsPublishReady') IS NULL
                    ALTER TABLE [dbo].[Templates] ADD [IsPublishReady] BIT NOT NULL
                        CONSTRAINT [DF_Templates_IsPublishReady] DEFAULT (0);
            ");

            migrationBuilder.Sql(@"
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
            ");

            migrationBuilder.Sql(@"
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
            ");

            migrationBuilder.Sql(@"
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

                    CREATE INDEX [IX_ReportDataUsageConsents_ReportMatchDraftId]
                        ON [dbo].[ReportDataUsageConsents] ([ReportMatchDraftId]);
                    CREATE INDEX [IX_ReportDataUsageConsents_ClientId]
                        ON [dbo].[ReportDataUsageConsents] ([ClientId]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportDataUsageConsents', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportDataUsageConsents];

                IF OBJECT_ID('dbo.ReportMatchColumnMappings', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportMatchColumnMappings];

                IF OBJECT_ID('dbo.ReportMatchDrafts', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportMatchDrafts];

                IF COL_LENGTH('dbo.Templates', 'IsPublishReady') IS NOT NULL
                    ALTER TABLE [dbo].[Templates] DROP COLUMN [IsPublishReady];

                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SchemaModels_Clients_SuggestedByClientId')
                    ALTER TABLE [dbo].[SchemaModels] DROP CONSTRAINT [FK_SchemaModels_Clients_SuggestedByClientId];

                IF COL_LENGTH('dbo.SchemaModels', 'SuggestedByClientId') IS NOT NULL
                    ALTER TABLE [dbo].[SchemaModels] DROP COLUMN [SuggestedByClientId];

                IF COL_LENGTH('dbo.SchemaModels', 'ReviewStatus') IS NOT NULL
                    ALTER TABLE [dbo].[SchemaModels] DROP COLUMN [ReviewStatus];

                IF COL_LENGTH('dbo.SchemaModels', 'IsAiSuggested') IS NOT NULL
                    ALTER TABLE [dbo].[SchemaModels] DROP COLUMN [IsAiSuggested];
            ");
        }
    }
}
