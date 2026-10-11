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
    [Migration("20260819120000_AddClientBrandingColorsAndFavicon")]
    public partial class AddClientBrandingColorsAndFavicon : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'BrandPrimaryColor') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [BrandPrimaryColor] NVARCHAR(9) NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'BrandSecondaryColor') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [BrandSecondaryColor] NVARCHAR(9) NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'FaviconBlobPath') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [FaviconBlobPath] NVARCHAR(500) NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'BrandPrimaryColor') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [BrandPrimaryColor];
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'BrandSecondaryColor') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [BrandSecondaryColor];
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'FaviconBlobPath') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [FaviconBlobPath];
            ");
        }
    }
}
