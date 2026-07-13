using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored: no `dotnet ef` tooling was available in the environment this was written in.
    // [Migration]/[DbContext] are declared directly on this class (normally emitted onto a
    // *.Designer.cs partial by tooling) instead of a separate Designer.cs file.
    //
    // KNOWN ISSUE (see MIGRATIONS.md): in production, Database.MigrateAsync() logged success
    // without actually creating this table — root cause not confirmed, missing [DbContext]
    // was the leading theory so it's added here now, but this has NOT been re-verified against
    // real EF tooling. HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync runs the same
    // guarded DDL directly and does not depend on this migration being picked up correctly —
    // treat that as the source of truth for whether these tables actually get created, not this
    // file's presence in the Migrations folder.
    [DbContext(typeof(ApplicationDbContext))]
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
