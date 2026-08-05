using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored, documentation/parity only — see MIGRATIONS.md and
    // 20260724010000_AddClientLogoBlobPath.cs for why.
    // HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync is the real source of truth for
    // schema changes.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260805020000_AddCustomReportRequestBlobExport")]
    public partial class AddCustomReportRequestBlobExport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CustomReportRequests', 'BlobPath') IS NULL
                    ALTER TABLE [dbo].[CustomReportRequests] ADD [BlobPath] NVARCHAR(1000) NULL;

                IF COL_LENGTH('dbo.CustomReportRequests', 'ExportedToBlobAtUtc') IS NULL
                    ALTER TABLE [dbo].[CustomReportRequests] ADD [ExportedToBlobAtUtc] DATETIME2 NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CustomReportRequests', 'ExportedToBlobAtUtc') IS NOT NULL
                    ALTER TABLE [dbo].[CustomReportRequests] DROP COLUMN [ExportedToBlobAtUtc];

                IF COL_LENGTH('dbo.CustomReportRequests', 'BlobPath') IS NOT NULL
                    ALTER TABLE [dbo].[CustomReportRequests] DROP COLUMN [BlobPath];
            ");
        }
    }
}
