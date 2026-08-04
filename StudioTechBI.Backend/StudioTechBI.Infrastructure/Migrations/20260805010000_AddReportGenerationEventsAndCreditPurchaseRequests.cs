using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored, kept for history/future `dotnet ef migrations add` reconciliation only —
    // HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync is the actual source of truth for
    // whether this schema exists (see MIGRATIONS.md for why hand-authored migrations alone were
    // not reliable in this environment).
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260805010000_AddReportGenerationEventsAndCreditPurchaseRequests")]
    public partial class AddReportGenerationEventsAndCreditPurchaseRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportGenerationEvents', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ReportGenerationEvents] (
                        [Id]                UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]          UNIQUEIDENTIFIER NOT NULL,
                        [Mode]              NVARCHAR(20)      NOT NULL,
                        [TemplateId]        NVARCHAR(200)     NULL,
                        [TemplateName]      NVARCHAR(200)     NULL,
                        [HtmlTemplateId]    NVARCHAR(200)     NULL,
                        [HtmlTemplateName]  NVARCHAR(200)     NULL,
                        [CreatedAt]         DATETIME2         NOT NULL,
                        [UpdatedAt]         DATETIME2         NULL,
                        [CreatedBy]         NVARCHAR(500)     NULL,
                        [UpdatedBy]         NVARCHAR(500)     NULL,
                        [IsDeleted]         BIT               NOT NULL,
                        CONSTRAINT [PK_ReportGenerationEvents] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ReportGenerationEvents_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_ReportGenerationEvents_ClientId_Mode]
                        ON [dbo].[ReportGenerationEvents] ([ClientId], [Mode]);
                END

                IF OBJECT_ID('dbo.CreditPurchaseRequests', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CreditPurchaseRequests] (
                        [Id]                 UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]           UNIQUEIDENTIFIER NOT NULL,
                        [RequestedByEmail]   NVARCHAR(320)     NULL,
                        [CreditsRequested]   INT               NOT NULL,
                        [PackLabel]          NVARCHAR(100)     NOT NULL,
                        [Status]             NVARCHAR(50)      NOT NULL,
                        [Notes]              NVARCHAR(2000)    NULL,
                        [PaidAtUtc]          DATETIME2         NULL,
                        [PaidByEmail]        NVARCHAR(320)     NULL,
                        [CreatedAt]          DATETIME2         NOT NULL,
                        [UpdatedAt]          DATETIME2         NULL,
                        [CreatedBy]          NVARCHAR(500)     NULL,
                        [UpdatedBy]          NVARCHAR(500)     NULL,
                        [IsDeleted]          BIT               NOT NULL,
                        CONSTRAINT [PK_CreditPurchaseRequests] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CreditPurchaseRequests_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_CreditPurchaseRequests_ClientId_Status]
                        ON [dbo].[CreditPurchaseRequests] ([ClientId], [Status]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.CreditPurchaseRequests', 'U') IS NOT NULL
                    DROP TABLE [dbo].[CreditPurchaseRequests];

                IF OBJECT_ID('dbo.ReportGenerationEvents', 'U') IS NOT NULL
                    DROP TABLE [dbo].[ReportGenerationEvents];
            ");
        }
    }
}
