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
    [Migration("20260724020000_AddClientIsPremiumSubscriber")]
    public partial class AddClientIsPremiumSubscriber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'IsPremiumSubscriber') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [IsPremiumSubscriber] BIT NOT NULL
                        CONSTRAINT [DF_Clients_IsPremiumSubscriber] DEFAULT (0);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'IsPremiumSubscriber') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [IsPremiumSubscriber];
            ");
        }
    }
}
