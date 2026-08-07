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
    [Migration("20260807010000_AddReportModelGeneration")]
    public partial class AddReportModelGeneration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportModelGenerations', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportModelGenerations];
            ");
        }
    }
}
