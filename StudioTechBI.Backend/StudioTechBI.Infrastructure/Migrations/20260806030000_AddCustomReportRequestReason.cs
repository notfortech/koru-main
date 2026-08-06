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
    [Migration("20260806030000_AddCustomReportRequestReason")]
    public partial class AddCustomReportRequestReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CustomReportRequests', 'RequestReason') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[CustomReportRequests] ADD [RequestReason] NVARCHAR(50) NULL;
                    UPDATE [dbo].[CustomReportRequests] SET [RequestReason] = 'NoConfidentMatch' WHERE [RequestReason] IS NULL;
                    ALTER TABLE [dbo].[CustomReportRequests] ALTER COLUMN [RequestReason] NVARCHAR(50) NOT NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CustomReportRequests', 'RequestReason') IS NOT NULL
                    ALTER TABLE [dbo].[CustomReportRequests] DROP COLUMN [RequestReason];
            ");
        }
    }
}
