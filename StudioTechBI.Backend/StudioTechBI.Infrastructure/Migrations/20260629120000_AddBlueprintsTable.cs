using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueprintsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddBlueprintModule (20260629000000) runs before this migration and already creates
            // the V2 Blueprints / BlueprintVersions / BlueprintGenerations tables.
            // This migration originally created the V1 Blueprints table (with BusinessRequirement,
            // ExistingSchema, credits columns) but its timestamp is LATER than AddBlueprintModule,
            // so the table always exists when this runs.  Guard the creation so MigrateAsync does
            // not abort and block the subsequent AddBlueprintVersionJsonContent migration.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.Blueprints', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Blueprints] (
                        [Id]               UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]         UNIQUEIDENTIFIER NOT NULL,
                        [BusinessRequirement] NVARCHAR(MAX) NOT NULL,
                        [Industry]         NVARCHAR(MAX) NOT NULL,
                        [ExistingSchema]   NVARCHAR(MAX) NULL,
                        [Status]           NVARCHAR(50)  NOT NULL,
                        [PdfDownloadUrl]   NVARCHAR(2000) NULL,
                        [CreditsConsumed]  INT NULL,
                        [CreditsRemaining] INT NULL,
                        [SubscriptionPlan] NVARCHAR(100) NULL,
                        [ResetDate]        DATETIMEOFFSET NULL,
                        [ResponseBlobPath] NVARCHAR(2000) NULL,
                        [WarningsJson]     NVARCHAR(MAX) NULL,
                        [RequestedByEmail] NVARCHAR(320) NULL,
                        [ErrorMessage]     NVARCHAR(MAX) NULL,
                        [CompletedAtUtc]   DATETIME2 NULL,
                        [CreatedAt]        DATETIME2 NOT NULL,
                        [UpdatedAt]        DATETIME2 NULL,
                        [CreatedBy]        NVARCHAR(MAX) NULL,
                        [UpdatedBy]        NVARCHAR(MAX) NULL,
                        [IsDeleted]        BIT NOT NULL,
                        CONSTRAINT [PK_Blueprints] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Blueprints_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_Blueprints_ClientId]
                        ON [dbo].[Blueprints] ([ClientId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only drop if this is the V1 table (no TenantId column).
            // If AddBlueprintModule has already replaced it with V2, leave it alone.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.Blueprints', 'U') IS NOT NULL
                   AND COL_LENGTH('dbo.Blueprints', 'BusinessRequirement') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[Blueprints];
                END
            ");
        }
    }
}
