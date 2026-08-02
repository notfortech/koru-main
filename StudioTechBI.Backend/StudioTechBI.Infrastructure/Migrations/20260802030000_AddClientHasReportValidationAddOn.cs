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
    [Migration("20260802030000_AddClientHasReportValidationAddOn")]
    public partial class AddClientHasReportValidationAddOn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'HasReportValidationAddOn') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [HasReportValidationAddOn] BIT NOT NULL
                        CONSTRAINT [DF_Clients_HasReportValidationAddOn] DEFAULT (0);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'HasReportValidationAddOn') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [HasReportValidationAddOn];
            ");
        }
    }
}
