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
    [Migration("20260818160000_AddUserTermsAcceptance")]
    public partial class AddUserTermsAcceptance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Users', 'TermsAcceptedAt') IS NULL
                    ALTER TABLE [dbo].[Users] ADD [TermsAcceptedAt] DATETIME2 NULL;

                IF COL_LENGTH('dbo.Users', 'TermsVersion') IS NULL
                    ALTER TABLE [dbo].[Users] ADD [TermsVersion] NVARCHAR(20) NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Users', 'TermsAcceptedAt') IS NOT NULL
                    ALTER TABLE [dbo].[Users] DROP COLUMN [TermsAcceptedAt];

                IF COL_LENGTH('dbo.Users', 'TermsVersion') IS NOT NULL
                    ALTER TABLE [dbo].[Users] DROP COLUMN [TermsVersion];
            ");
        }
    }
}
