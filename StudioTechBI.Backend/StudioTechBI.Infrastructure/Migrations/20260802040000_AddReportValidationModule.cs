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
    [Migration("20260802040000_AddReportValidationModule")]
    public partial class AddReportValidationModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportValidationRuns', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ReportValidationRuns] (
                        [Id]                         UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]                   UNIQUEIDENTIFIER NOT NULL,
                        [RequestedByUserId]          UNIQUEIDENTIFIER NOT NULL,
                        [Status]                     NVARCHAR(20)      NOT NULL,
                        [OverallResult]              NVARCHAR(20)      NULL,
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
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportValidationChecks', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportValidationChecks];

                IF OBJECT_ID('dbo.ReportValidationRuns', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportValidationRuns];
            ");
        }
    }
}
