using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    /// <summary>
    /// Aligns the EF model with the actual database (see schema): Templates.ModelId = uniqueidentifier,
    /// PowerBI id columns, InsightModels extra columns, ModelConsents. Up is idempotent for servers that
    /// already have these objects (e.g. from an earlier apply).
    /// </summary>
    public partial class AlignTemplateAndPowerBiWithDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Templates', 'ModelId') IS NULL
                    ALTER TABLE [dbo].[Templates] ADD [ModelId] uniqueidentifier NULL;
                IF COL_LENGTH('dbo.Templates', 'OptionalColumnsJson') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Templates] ADD [OptionalColumnsJson] nvarchar(max) NOT NULL
                        CONSTRAINT [DF_Templates_OptionalColumnsJson] DEFAULT (N'[]');
                END
                IF COL_LENGTH('dbo.Templates', 'RequiredColumnsJson') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Templates] ADD [RequiredColumnsJson] nvarchar(max) NOT NULL
                        CONSTRAINT [DF_Templates_RequiredColumnsJson] DEFAULT (N'[]');
                END
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.InsightModels', 'ApprovedAt') IS NULL
                    ALTER TABLE [dbo].[InsightModels] ADD [ApprovedAt] datetime2 NULL;
                IF COL_LENGTH('dbo.InsightModels', 'ExternalModelId') IS NULL
                    ALTER TABLE [dbo].[InsightModels] ADD [ExternalModelId] nvarchar(200) NULL;
                IF COL_LENGTH('dbo.InsightModels', 'TomJson') IS NULL
                    ALTER TABLE [dbo].[InsightModels] ADD [TomJson] nvarchar(max) NULL;
                """);

            // reporting.PowerBiAssets: use idempotent T-SQL (EF AlterColumn can fail on Azure if DB already
            // matches production schema, or if redundant ALTER is rejected; avoids startup crash on MigrateAsync)
            migrationBuilder.Sql(
                """
                DECLARE @oid int = OBJECT_ID(N'reporting.PowerBiAssets');
                IF @oid IS NULL RETURN;

                -- nvarchar(100) NOT NULL (fill NULLs for safe ALTER)
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @oid AND name = N'WorkspaceId' AND is_nullable = 1)
                BEGIN
                    UPDATE [reporting].[PowerBIAssets] SET [WorkspaceId] = N'' WHERE [WorkspaceId] IS NULL;
                    ALTER TABLE [reporting].[PowerBIAssets] ALTER COLUMN [WorkspaceId] nvarchar(100) NOT NULL;
                END
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @oid AND name = N'DatasetId' AND is_nullable = 1)
                BEGIN
                    UPDATE [reporting].[PowerBIAssets] SET [DatasetId] = N'' WHERE [DatasetId] IS NULL;
                    ALTER TABLE [reporting].[PowerBIAssets] ALTER COLUMN [DatasetId] nvarchar(100) NOT NULL;
                END
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @oid AND name = N'ReportId' AND is_nullable = 1)
                BEGIN
                    UPDATE [reporting].[PowerBIAssets] SET [ReportId] = N'' WHERE [ReportId] IS NULL;
                    ALTER TABLE [reporting].[PowerBIAssets] ALTER COLUMN [ReportId] nvarchar(100) NOT NULL;
                END

                -- TenantId NOT NULL
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @oid AND name = N'TenantId' AND is_nullable = 1)
                BEGIN
                    UPDATE [reporting].[PowerBIAssets] SET [TenantId] = '00000000-0000-0000-0000-000000000000' WHERE [TenantId] IS NULL;
                    ALTER TABLE [reporting].[PowerBIAssets] ALTER COLUMN [TenantId] uniqueidentifier NOT NULL;
                END

                -- IsActive: nullable bit (was modelled non-nullable in older snapshot)
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @oid AND name = N'IsActive' AND is_nullable = 0)
                    ALTER TABLE [reporting].[PowerBIAssets] ALTER COLUMN [IsActive] bit NULL;
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InsightModels_ClientId_ExternalModelId' AND object_id = OBJECT_ID(N'dbo.InsightModels'))
                    CREATE NONCLUSTERED INDEX [IX_InsightModels_ClientId_ExternalModelId] ON [dbo].[InsightModels] ([ClientId], [ExternalModelId]);
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[ModelConsents]', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ModelConsents] (
                        [Id] uniqueidentifier NOT NULL,
                        [ModelId] uniqueidentifier NOT NULL,
                        [ClientId] uniqueidentifier NOT NULL,
                        [ApprovedAt] datetime2 NOT NULL,
                        [SummaryJson] nvarchar(max) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [IsDeleted] bit NOT NULL,
                        CONSTRAINT [PK_ModelConsents] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ModelConsents_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_ModelConsents_InsightModels_ModelId] FOREIGN KEY ([ModelId]) REFERENCES [dbo].[InsightModels] ([Id]) ON DELETE CASCADE
                    );
                END
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ModelConsents_ClientId' AND object_id = OBJECT_ID(N'dbo.ModelConsents'))
                    CREATE NONCLUSTERED INDEX [IX_ModelConsents_ClientId] ON [dbo].[ModelConsents] ([ClientId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ModelConsents_ModelId' AND object_id = OBJECT_ID(N'dbo.ModelConsents'))
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_ModelConsents_ModelId] ON [dbo].[ModelConsents] ([ModelId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ModelConsents_ModelId' AND object_id = OBJECT_ID(N'dbo.ModelConsents'))
                    DROP INDEX [IX_ModelConsents_ModelId] ON [dbo].[ModelConsents];
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ModelConsents_ClientId' AND object_id = OBJECT_ID(N'dbo.ModelConsents'))
                    DROP INDEX [IX_ModelConsents_ClientId] ON [dbo].[ModelConsents];
                """);

            migrationBuilder.Sql(
                "IF OBJECT_ID(N'[dbo].[ModelConsents]', 'U') IS NOT NULL DROP TABLE [dbo].[ModelConsents];");

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InsightModels_ClientId_ExternalModelId' AND object_id = OBJECT_ID(N'dbo.InsightModels'))
                    DROP INDEX [IX_InsightModels_ClientId_ExternalModelId] ON [dbo].[InsightModels];
                """);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceId",
                schema: "reporting",
                table: "PowerBiAssets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "reporting",
                table: "PowerBiAssets",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ReportId",
                schema: "reporting",
                table: "PowerBiAssets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "reporting",
                table: "PowerBiAssets",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DatasetId",
                schema: "reporting",
                table: "PowerBiAssets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.InsightModels', 'TomJson') IS NOT NULL
                    ALTER TABLE [dbo].[InsightModels] DROP COLUMN [TomJson];
                IF COL_LENGTH('dbo.InsightModels', 'ExternalModelId') IS NOT NULL
                    ALTER TABLE [dbo].[InsightModels] DROP COLUMN [ExternalModelId];
                IF COL_LENGTH('dbo.InsightModels', 'ApprovedAt') IS NOT NULL
                    ALTER TABLE [dbo].[InsightModels] DROP COLUMN [ApprovedAt];
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Templates', 'ModelId') IS NOT NULL
                    ALTER TABLE [dbo].[Templates] DROP COLUMN [ModelId];
                IF COL_LENGTH('dbo.Templates', 'OptionalColumnsJson') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Templates] DROP CONSTRAINT IF EXISTS [DF_Templates_OptionalColumnsJson];
                    ALTER TABLE [dbo].[Templates] DROP COLUMN [OptionalColumnsJson];
                END
                IF COL_LENGTH('dbo.Templates', 'RequiredColumnsJson') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Templates] DROP CONSTRAINT IF EXISTS [DF_Templates_RequiredColumnsJson];
                    ALTER TABLE [dbo].[Templates] DROP COLUMN [RequiredColumnsJson];
                END
                """);
        }
    }
}
