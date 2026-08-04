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
    [Migration("20260804010000_AddSavedReportsAndCustomReportRequests")]
    public partial class AddSavedReportsAndCustomReportRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.SavedReports', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SavedReports] (
                        [Id]              UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]        UNIQUEIDENTIFIER NOT NULL,
                        [Title]           NVARCHAR(300)     NOT NULL,
                        [SourceType]      NVARCHAR(50)      NOT NULL,
                        [Status]          NVARCHAR(50)      NOT NULL,
                        [VersionCount]    INT               NOT NULL,
                        [PowerBiAssetId]  UNIQUEIDENTIFIER  NULL,
                        [CreatedAt]       DATETIME2         NOT NULL,
                        [UpdatedAt]       DATETIME2         NULL,
                        [CreatedBy]       NVARCHAR(500)     NULL,
                        [UpdatedBy]       NVARCHAR(500)     NULL,
                        [IsDeleted]       BIT               NOT NULL,
                        CONSTRAINT [PK_SavedReports] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_SavedReports_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_SavedReports_ClientId_Status] ON [dbo].[SavedReports] ([ClientId], [Status]);
                END

                IF OBJECT_ID('dbo.SavedReportVersions', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SavedReportVersions] (
                        [Id]                  UNIQUEIDENTIFIER NOT NULL,
                        [SavedReportId]       UNIQUEIDENTIFIER NOT NULL,
                        [VersionNumber]       INT               NOT NULL,
                        [HtmlBlobPath]        NVARCHAR(1000)    NULL,
                        [TemplateId]          NVARCHAR(200)     NULL,
                        [TemplateName]        NVARCHAR(200)     NULL,
                        [HtmlTemplateId]      NVARCHAR(200)     NULL,
                        [HtmlTemplateName]    NVARCHAR(200)     NULL,
                        [SourceFileName]      NVARCHAR(500)     NULL,
                        [AppliedFiltersJson]  NVARCHAR(MAX)     NULL,
                        [GeneratedDate]       DATETIME2         NOT NULL,
                        [IsActive]            BIT               NOT NULL,
                        [CreatedAt]           DATETIME2         NOT NULL,
                        [UpdatedAt]           DATETIME2         NULL,
                        [CreatedBy]           NVARCHAR(500)     NULL,
                        [UpdatedBy]           NVARCHAR(500)     NULL,
                        [IsDeleted]           BIT               NOT NULL,
                        CONSTRAINT [PK_SavedReportVersions] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_SavedReportVersions_SavedReports_SavedReportId]
                            FOREIGN KEY ([SavedReportId]) REFERENCES [dbo].[SavedReports] ([Id])
                            ON DELETE CASCADE
                    );

                    CREATE INDEX [IX_SavedReportVersions_SavedReportId_IsActive]
                        ON [dbo].[SavedReportVersions] ([SavedReportId], [IsActive]);
                END

                IF OBJECT_ID('dbo.CustomReportRequests', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CustomReportRequests] (
                        [Id]                     UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]               UNIQUEIDENTIFIER NOT NULL,
                        [Status]                 NVARCHAR(50)      NOT NULL,
                        [RequestedByEmail]       NVARCHAR(320)     NULL,
                        [Notes]                  NVARCHAR(2000)    NULL,
                        [SchemaSnapshotJson]     NVARCHAR(MAX)     NOT NULL,
                        [SourceFileName]         NVARCHAR(500)     NULL,
                        [FulfilledSavedReportId] UNIQUEIDENTIFIER  NULL,
                        [FulfilledAtUtc]         DATETIME2         NULL,
                        [FulfilledByEmail]       NVARCHAR(320)     NULL,
                        [CreatedAt]              DATETIME2         NOT NULL,
                        [UpdatedAt]              DATETIME2         NULL,
                        [CreatedBy]              NVARCHAR(500)     NULL,
                        [UpdatedBy]              NVARCHAR(500)     NULL,
                        [IsDeleted]              BIT               NOT NULL,
                        CONSTRAINT [PK_CustomReportRequests] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CustomReportRequests_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_CustomReportRequests_ClientId_Status]
                        ON [dbo].[CustomReportRequests] ([ClientId], [Status]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.CustomReportRequests', 'U') IS NOT NULL
                    DROP TABLE [dbo].[CustomReportRequests];

                IF OBJECT_ID('dbo.SavedReportVersions', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SavedReportVersions];

                IF OBJECT_ID('dbo.SavedReports', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SavedReports];
            ");
        }
    }
}
