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
    [Migration("20260805030000_AddLocalCreditLedger")]
    public partial class AddLocalCreditLedger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Clients', 'AiCreditsRemaining') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [AiCreditsRemaining] INT NOT NULL
                        CONSTRAINT [DF_Clients_AiCreditsRemaining] DEFAULT (1000);

                IF COL_LENGTH('dbo.CreditPurchaseRequests', 'Source') IS NULL
                    ALTER TABLE [dbo].[CreditPurchaseRequests] ADD [Source] NVARCHAR(20) NOT NULL
                        CONSTRAINT [DF_CreditPurchaseRequests_Source] DEFAULT ('Client');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.CreditPurchaseRequests', 'Source') IS NOT NULL
                    ALTER TABLE [dbo].[CreditPurchaseRequests] DROP COLUMN [Source];

                IF COL_LENGTH('dbo.Clients', 'AiCreditsRemaining') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [AiCreditsRemaining];
            ");
        }
    }
}
