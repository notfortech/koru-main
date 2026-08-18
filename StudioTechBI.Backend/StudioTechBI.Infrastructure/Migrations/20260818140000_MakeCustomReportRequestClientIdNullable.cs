using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored, documentation/parity only — see MIGRATIONS.md and
    // 20260715020000_AddSchemaModelFieldAliases.cs for why.
    // HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync is the real source of truth for
    // schema changes.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260818140000_MakeCustomReportRequestClientIdNullable")]
    public partial class MakeCustomReportRequestClientIdNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('dbo.CustomReportRequests') AND name = 'ClientId' AND is_nullable = 0
                )
                    ALTER TABLE [dbo].[CustomReportRequests] ALTER COLUMN [ClientId] UNIQUEIDENTIFIER NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('dbo.CustomReportRequests') AND name = 'ClientId' AND is_nullable = 1
                )
                    ALTER TABLE [dbo].[CustomReportRequests] ALTER COLUMN [ClientId] UNIQUEIDENTIFIER NOT NULL;
            ");
        }
    }
}
