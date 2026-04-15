using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations;

/// <summary>Adds InsightEngine persistence only (InsightModels, InsightDatasets). Other schema is covered by earlier migrations.</summary>
public partial class AddInsightEngineTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InsightModels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ExcelSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ConfidenceScore = table.Column<double>(type: "float", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InsightModels", x => x.Id);
                table.ForeignKey(
                    name: "FK_InsightModels_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InsightDatasets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PowerBIDatasetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ReportId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InsightDatasets", x => x.Id);
                table.ForeignKey(
                    name: "FK_InsightDatasets_InsightModels_ModelId",
                    column: x => x.ModelId,
                    principalTable: "InsightModels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InsightDatasets_ModelId",
            table: "InsightDatasets",
            column: "ModelId");

        migrationBuilder.CreateIndex(
            name: "IX_InsightModels_ClientId",
            table: "InsightModels",
            column: "ClientId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InsightDatasets");
        migrationBuilder.DropTable(name: "InsightModels");
    }
}
