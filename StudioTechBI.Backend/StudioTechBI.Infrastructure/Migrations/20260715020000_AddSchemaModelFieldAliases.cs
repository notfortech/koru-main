using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored — see 20260712120000_AddReportDesignerConsent.cs and
    // 20260712130000_AddSchemaModelLibrary.cs for why there's no Designer.cs partial and why
    // this isn't confirmed to actually run via Database.MigrateAsync() in production.
    // HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync is the real source of truth for
    // table creation — this file exists so a future `dotnet ef migrations add` has a consistent
    // history to diff against.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260715020000_AddSchemaModelFieldAliases")]
    public partial class AddSchemaModelFieldAliases : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.SchemaModelFieldAliases', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SchemaModelFieldAliases] (
                        [Id]                          UNIQUEIDENTIFIER NOT NULL,
                        [SchemaModelFieldId]          UNIQUEIDENTIFIER NOT NULL,
                        [AliasName]                   NVARCHAR(200)     NOT NULL,
                        [NormalizedAliasName]         NVARCHAR(200)     NOT NULL,
                        [ObservedDataType]            NVARCHAR(50)      NULL,
                        [Confidence]                  FLOAT             NOT NULL,
                        [Source]                      NVARCHAR(20)      NOT NULL,
                        [ApprovalStatus]              NVARCHAR(20)      NOT NULL,
                        [ObservedCount]                INT              NOT NULL,
                        [FirstSeenAt]                 DATETIME2         NOT NULL,
                        [LastSeenAt]                  DATETIME2         NOT NULL,
                        [FirstSeenClientId]           UNIQUEIDENTIFIER  NULL,
                        [FirstSeenReportMatchDraftId] UNIQUEIDENTIFIER  NULL,
                        [DecidedBy]                   NVARCHAR(256)     NULL,
                        [DecidedAt]                   DATETIME2         NULL,
                        [CreatedAt]                   DATETIME2         NOT NULL,
                        [UpdatedAt]                   DATETIME2         NULL,
                        [CreatedBy]                   NVARCHAR(MAX)     NULL,
                        [UpdatedBy]                   NVARCHAR(MAX)     NULL,
                        [IsDeleted]                   BIT               NOT NULL,
                        CONSTRAINT [PK_SchemaModelFieldAliases] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_SchemaModelFieldAliases_SchemaModelFields_SchemaModelFieldId]
                            FOREIGN KEY ([SchemaModelFieldId]) REFERENCES [dbo].[SchemaModelFields] ([Id])
                            ON DELETE CASCADE,
                        CONSTRAINT [FK_SchemaModelFieldAliases_ReportMatchDrafts_FirstSeenReportMatchDraftId]
                            FOREIGN KEY ([FirstSeenReportMatchDraftId]) REFERENCES [dbo].[ReportMatchDrafts] ([Id])
                            ON DELETE SET NULL
                    );

                    CREATE UNIQUE INDEX [IX_SchemaModelFieldAliases_FieldId_NormalizedAliasName]
                        ON [dbo].[SchemaModelFieldAliases] ([SchemaModelFieldId], [NormalizedAliasName]);
                    CREATE INDEX [IX_SchemaModelFieldAliases_NormalizedAliasName_ApprovalStatus]
                        ON [dbo].[SchemaModelFieldAliases] ([NormalizedAliasName], [ApprovalStatus]);
                    CREATE INDEX [IX_SchemaModelFieldAliases_ApprovalStatus]
                        ON [dbo].[SchemaModelFieldAliases] ([ApprovalStatus]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.SchemaModelFieldAliases', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SchemaModelFieldAliases];
            ");
        }
    }
}
