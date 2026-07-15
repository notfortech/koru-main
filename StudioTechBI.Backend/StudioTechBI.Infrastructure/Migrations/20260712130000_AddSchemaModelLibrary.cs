using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    // Hand-authored — see 20260712120000_AddReportDesignerConsent.cs for why there's no
    // Designer.cs partial, and MIGRATIONS.md for the production issue this caused: this
    // migration was not actually applied despite Database.MigrateAsync() logging success.
    // HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync is the real source of truth for
    // table creation now — this file is kept so a future `dotnet ef migrations add` has a
    // consistent history to diff against, not because it's confirmed to run.
    [DbContext(typeof(ApplicationDbContext))]
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

            // NOTE (corrected 2026-07-13, see MIGRATIONS.md): this originally added a second FK
            // on Templates.ModelId, not realizing production already has an unrelated dbo.Models
            // table with its own FK_Templates_Models on that same column — every insert then had
            // to satisfy both, which failed for every SchemaModel-linked row. SchemaModel linkage
            // now gets its own column; Templates.ModelId is left alone entirely.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Templates', 'SchemaModelId') IS NULL
                    ALTER TABLE [dbo].[Templates] ADD [SchemaModelId] UNIQUEIDENTIFIER NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Templates_SchemaModelId')
                    CREATE INDEX [IX_Templates_SchemaModelId] ON [dbo].[Templates] ([SchemaModelId]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Templates_SchemaModels_SchemaModelId'
                )
                BEGIN
                    ALTER TABLE [dbo].[Templates] WITH NOCHECK
                        ADD CONSTRAINT [FK_Templates_SchemaModels_SchemaModelId]
                        FOREIGN KEY ([SchemaModelId]) REFERENCES [dbo].[SchemaModels] ([Id])
                        ON DELETE SET NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Templates_SchemaModels_SchemaModelId')
                    ALTER TABLE [dbo].[Templates] DROP CONSTRAINT [FK_Templates_SchemaModels_SchemaModelId];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Templates_SchemaModelId')
                    DROP INDEX [IX_Templates_SchemaModelId] ON [dbo].[Templates];

                IF COL_LENGTH('dbo.Templates', 'SchemaModelId') IS NOT NULL
                    ALTER TABLE [dbo].[Templates] DROP COLUMN [SchemaModelId];

                IF OBJECT_ID('dbo.SchemaModelFields', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SchemaModelFields];

                IF OBJECT_ID('dbo.SchemaModels', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SchemaModels];
            ");
        }
    }
}
