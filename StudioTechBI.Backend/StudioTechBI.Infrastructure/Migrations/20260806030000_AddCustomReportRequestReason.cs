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
            // Split into separate batches -- SQL Server compiles a whole migrationBuilder.Sql()
            // call as one batch against the pre-batch schema snapshot, so a same-batch ADD
            // followed by UPDATE/ALTER COLUMN referencing the new column fails with
            // "Invalid column name" (Error 207) even though the ADD itself is valid.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CustomReportRequests', 'RequestReason') IS NULL
                    ALTER TABLE [dbo].[CustomReportRequests] ADD [RequestReason] NVARCHAR(50) NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE [dbo].[CustomReportRequests] SET [RequestReason] = 'NoConfidentMatch' WHERE [RequestReason] IS NULL;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('dbo.CustomReportRequests') AND name = 'RequestReason' AND is_nullable = 1
                )
                    ALTER TABLE [dbo].[CustomReportRequests] ALTER COLUMN [RequestReason] NVARCHAR(50) NOT NULL;
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
