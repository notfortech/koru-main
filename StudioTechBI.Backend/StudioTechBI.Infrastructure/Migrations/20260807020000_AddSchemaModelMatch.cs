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
    [Migration("20260807020000_AddSchemaModelMatch")]
    public partial class AddSchemaModelMatch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.SchemaModelMatches', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SchemaModelMatches] (
                        [Id]                   UNIQUEIDENTIFIER NOT NULL,
                        [ClientId]             UNIQUEIDENTIFIER NOT NULL,
                        [RequestId]            NVARCHAR(100)     NOT NULL,
                        [Status]               NVARCHAR(20)      NOT NULL,
                        [RequestPayloadJson]   NVARCHAR(MAX)     NOT NULL,
                        [ResponseJson]         NVARCHAR(MAX)     NULL,
                        [ErrorMessage]         NVARCHAR(MAX)     NULL,
                        [ProcessingStartedAt]  DATETIME2         NULL,
                        [CompletedAt]          DATETIME2         NULL,
                        [CreatedAt]            DATETIME2         NOT NULL,
                        [UpdatedAt]            DATETIME2         NULL,
                        [CreatedBy]            NVARCHAR(500)     NULL,
                        [UpdatedBy]            NVARCHAR(500)     NULL,
                        [IsDeleted]            BIT               NOT NULL,
                        CONSTRAINT [PK_SchemaModelMatches] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_SchemaModelMatches_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_SchemaModelMatches_ClientId] ON [dbo].[SchemaModelMatches] ([ClientId]);
                    CREATE INDEX [IX_SchemaModelMatches_Status] ON [dbo].[SchemaModelMatches] ([Status]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.SchemaModelMatches', 'U') IS NOT NULL
                    DROP TABLE [dbo].[SchemaModelMatches];
            ");
        }
    }
}
