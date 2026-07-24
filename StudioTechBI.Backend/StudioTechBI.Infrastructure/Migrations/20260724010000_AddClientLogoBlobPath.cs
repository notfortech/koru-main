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
    [Migration("20260724010000_AddClientLogoBlobPath")]
    public partial class AddClientLogoBlobPath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'LogoBlobPath') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [LogoBlobPath] NVARCHAR(500) NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'LogoBlobPath') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [LogoBlobPath];
            ");
        }
    }
}
