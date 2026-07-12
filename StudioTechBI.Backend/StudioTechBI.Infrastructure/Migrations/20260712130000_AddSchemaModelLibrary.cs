using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored — see 20260712120000_AddReportDesignerConsent.cs for why there's no
    // Designer.cs partial. Regenerate a snapshot-synced migration with `dotnet ef migrations add`
    // next time the SDK is available.
    [Migration("20260712130000_AddSchemaModelLibrary")]
    public partial class AddSchemaModelLibrary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.SchemaModels', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SchemaModels] (
                        [Id]          UNIQUEIDENTIFIER NOT NULL,
                        [Name]        NVARCHAR(200)     NOT NULL,
                        [Industry]    NVARCHAR(100)     NOT NULL,
                        [Description] NVARCHAR(1000)    NULL,
                        [CreatedAt]   DATETIME2         NOT NULL,
                        [UpdatedAt]   DATETIME2         NULL,
                        [CreatedBy]   NVARCHAR(MAX)     NULL,
                        [UpdatedBy]   NVARCHAR(MAX)     NULL,
                        [IsDeleted]   BIT               NOT NULL,
                        CONSTRAINT [PK_SchemaModels] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_SchemaModels_Name] ON [dbo].[SchemaModels] ([Name]);
                    CREATE INDEX [IX_SchemaModels_Industry] ON [dbo].[SchemaModels] ([Industry]);
                END
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.SchemaModelFields', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SchemaModelFields] (
                        [Id]            UNIQUEIDENTIFIER NOT NULL,
                        [SchemaModelId] UNIQUEIDENTIFIER NOT NULL,
                        [FieldName]     NVARCHAR(200)     NOT NULL,
                        [DataType]      NVARCHAR(50)      NOT NULL,
                        [IsRequired]    BIT               NOT NULL,
                        [SortOrder]     INT               NOT NULL,
                        [CreatedAt]     DATETIME2         NOT NULL,
                        [UpdatedAt]     DATETIME2         NULL,
                        [CreatedBy]     NVARCHAR(MAX)     NULL,
                        [UpdatedBy]     NVARCHAR(MAX)     NULL,
                        [IsDeleted]     BIT               NOT NULL,
                        CONSTRAINT [PK_SchemaModelFields] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_SchemaModelFields_SchemaModels_SchemaModelId]
                            FOREIGN KEY ([SchemaModelId]) REFERENCES [dbo].[SchemaModels] ([Id])
                            ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX [IX_SchemaModelFields_SchemaModelId_FieldName]
                        ON [dbo].[SchemaModelFields] ([SchemaModelId], [FieldName]);
                END
            ");

            // Templates.ModelId already exists (uniqueidentifier NULL, unenforced) and may hold
            // values from before SchemaModels existed. Add the FK WITH NOCHECK so pre-existing,
            // unresolvable ModelIds don't block the migration — only new/updated rows are
            // enforced going forward. This migration does not attempt to backfill old rows.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Templates_SchemaModels_ModelId'
                )
                BEGIN
                    CREATE INDEX [IX_Templates_ModelId] ON [dbo].[Templates] ([ModelId]);

                    ALTER TABLE [dbo].[Templates] WITH NOCHECK
                        ADD CONSTRAINT [FK_Templates_SchemaModels_ModelId]
                        FOREIGN KEY ([ModelId]) REFERENCES [dbo].[SchemaModels] ([Id])
                        ON DELETE SET NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Templates_SchemaModels_ModelId')
                    ALTER TABLE [dbo].[Templates] DROP CONSTRAINT [FK_Templates_SchemaModels_ModelId];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Templates_ModelId')
                    DROP INDEX [IX_Templates_ModelId] ON [dbo].[Templates];

                IF OBJECT_ID('dbo.SchemaModelFields', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SchemaModelFields];

                IF OBJECT_ID('dbo.SchemaModels', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SchemaModels];
            ");
        }
    }
}
