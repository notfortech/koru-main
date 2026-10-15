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
    [Migration("20261015090000_AddCreditPurchaseRequestStripeCheckoutSessionId")]
    public partial class AddCreditPurchaseRequestStripeCheckoutSessionId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CreditPurchaseRequests', 'StripeCheckoutSessionId') IS NULL
                    ALTER TABLE [dbo].[CreditPurchaseRequests] ADD [StripeCheckoutSessionId] NVARCHAR(255) NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CreditPurchaseRequests', 'StripeCheckoutSessionId') IS NOT NULL
                    ALTER TABLE [dbo].[CreditPurchaseRequests] DROP COLUMN [StripeCheckoutSessionId];
            ");
        }
    }
}
