using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored, documentation/parity only — see MIGRATIONS.md and
    // 20260715020000_AddSchemaModelFieldAliases.cs for why.
    // HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync is the real source of truth for
    // table creation.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260716030000_AddAiBoundaryAuditEvents")]
    public partial class AddAiBoundaryAuditEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.AiBoundaryAuditEvents', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AiBoundaryAuditEvents] (
                        [Id]             UNIQUEIDENTIFIER NOT NULL,
                        [CorrelationId]  NVARCHAR(100)     NOT NULL,
                        [Service]        NVARCHAR(100)     NOT NULL,
                        [Operation]      NVARCHAR(100)     NOT NULL,
                        [Phase]          NVARCHAR(20)      NOT NULL,
                        [TargetService]  NVARCHAR(100)     NOT NULL,
                        [MetadataJson]   NVARCHAR(MAX)     NOT NULL,
                        [DurationMs]     BIGINT            NULL,
                        [StatusCode]     INT               NULL,
                        [Success]        BIT               NULL,
                        [ErrorSummary]   NVARCHAR(500)     NULL,
                        [ClientId]       UNIQUEIDENTIFIER  NULL,
                        [CreatedAt]      DATETIME2         NOT NULL,
                        [UpdatedAt]      DATETIME2         NULL,
                        [CreatedBy]      NVARCHAR(MAX)     NULL,
                        [UpdatedBy]      NVARCHAR(MAX)     NULL,
                        [IsDeleted]      BIT               NOT NULL,
                        CONSTRAINT [PK_AiBoundaryAuditEvents] PRIMARY KEY ([Id])
                    );

                    CREATE INDEX [IX_AiBoundaryAuditEvents_CorrelationId]
                        ON [dbo].[AiBoundaryAuditEvents] ([CorrelationId]);
                    CREATE INDEX [IX_AiBoundaryAuditEvents_Operation_CreatedAt]
                        ON [dbo].[AiBoundaryAuditEvents] ([Operation], [CreatedAt]);
                    CREATE INDEX [IX_AiBoundaryAuditEvents_Success]
                        ON [dbo].[AiBoundaryAuditEvents] ([Success]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.AiBoundaryAuditEvents', 'U') IS NOT NULL
                    DROP TABLE [dbo].[AiBoundaryAuditEvents];
            ");
        }
    }
}
