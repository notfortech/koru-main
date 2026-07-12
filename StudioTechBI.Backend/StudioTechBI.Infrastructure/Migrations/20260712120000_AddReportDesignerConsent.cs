using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored: no `dotnet ef` tooling was available in the environment this was written in.
    // [Migration] is declared directly on this class (normally emitted onto a *.Designer.cs
    // partial by tooling) since Migration discovery/apply at runtime only needs this attribute —
    // it does not require a Designer.cs snapshot. Regenerate a proper snapshot-synced migration
    // with `dotnet ef migrations add` next time the SDK is available; this table's creation is
    // guarded so that re-sync will no-op against it, same approach already used by
    // 20260629120000_AddBlueprintsTable.cs.
    [Migration("20260712120000_AddReportDesignerConsent")]
    public partial class AddReportDesignerConsent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportDesignerConsents', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ReportDesignerConsents] (
                        [Id]           UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]     UNIQUEIDENTIFIER NOT NULL,
                        [SchemaHash]   NVARCHAR(128)     NOT NULL,
                        [ApprovedAt]   DATETIME2         NOT NULL,
                        [ApprovedBy]   NVARCHAR(256)     NOT NULL,
                        [CreatedAt]    DATETIME2         NOT NULL,
                        [UpdatedAt]    DATETIME2         NULL,
                        [CreatedBy]    NVARCHAR(MAX)     NULL,
                        [UpdatedBy]    NVARCHAR(MAX)     NULL,
                        [IsDeleted]    BIT               NOT NULL,
                        CONSTRAINT [PK_ReportDesignerConsents] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ReportDesignerConsents_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                            ON DELETE NO ACTION
                    );

                    CREATE UNIQUE INDEX [IX_ReportDesignerConsents_ClientId_SchemaHash]
                        ON [dbo].[ReportDesignerConsents] ([ClientId], [SchemaHash]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.ReportDesignerConsents', 'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[ReportDesignerConsents];
                END
            ");
        }
    }
}
