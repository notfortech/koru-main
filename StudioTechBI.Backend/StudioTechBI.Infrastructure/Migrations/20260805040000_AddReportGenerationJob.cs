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
    [Migration("20260805040000_AddReportGenerationJob")]
    public partial class AddReportGenerationJob : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportGenerationJobs', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportGenerationJobs];
            ");
        }
    }
}
