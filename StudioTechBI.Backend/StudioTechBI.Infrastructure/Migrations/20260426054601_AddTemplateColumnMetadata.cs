using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateColumnMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: Some environments may already have Templates.ModelId created.
            // Use SQL guards to make this migration idempotent across databases.
            migrationBuilder.Sql("""
IF COL_LENGTH('Templates', 'ModelId') IS NULL
BEGIN
    ALTER TABLE [Templates] ADD [ModelId] nvarchar(200) NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('Templates', 'OptionalColumnsJson') IS NULL
BEGIN
    ALTER TABLE [Templates] ADD [OptionalColumnsJson] nvarchar(max) NOT NULL CONSTRAINT [DF_Templates_OptionalColumnsJson] DEFAULT(N'[]');
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('Templates', 'RequiredColumnsJson') IS NULL
BEGIN
    ALTER TABLE [Templates] ADD [RequiredColumnsJson] nvarchar(max) NOT NULL CONSTRAINT [DF_Templates_RequiredColumnsJson] DEFAULT(N'[]');
END
""");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "InsightModels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalModelId",
                table: "InsightModels",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TomJson",
                table: "InsightModels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelConsents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelConsents_InsightModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "InsightModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InsightModels_ClientId_ExternalModelId",
                table: "InsightModels",
                columns: new[] { "ClientId", "ExternalModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelConsents_ClientId",
                table: "ModelConsents",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelConsents_ModelId",
                table: "ModelConsents",
                column: "ModelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelConsents");

            migrationBuilder.DropIndex(
                name: "IX_InsightModels_ClientId_ExternalModelId",
                table: "InsightModels");

            // Best-effort rollback (guarded).
            migrationBuilder.Sql("""
IF COL_LENGTH('Templates', 'OptionalColumnsJson') IS NOT NULL
BEGIN
    ALTER TABLE [Templates] DROP CONSTRAINT IF EXISTS [DF_Templates_OptionalColumnsJson];
    ALTER TABLE [Templates] DROP COLUMN [OptionalColumnsJson];
END
IF COL_LENGTH('Templates', 'RequiredColumnsJson') IS NOT NULL
BEGIN
    ALTER TABLE [Templates] DROP CONSTRAINT IF EXISTS [DF_Templates_RequiredColumnsJson];
    ALTER TABLE [Templates] DROP COLUMN [RequiredColumnsJson];
END
IF COL_LENGTH('Templates', 'ModelId') IS NOT NULL
BEGIN
    ALTER TABLE [Templates] DROP COLUMN [ModelId];
END
""");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "InsightModels");

            migrationBuilder.DropColumn(
                name: "ExternalModelId",
                table: "InsightModels");

            migrationBuilder.DropColumn(
                name: "TomJson",
                table: "InsightModels");
        }
    }
}
