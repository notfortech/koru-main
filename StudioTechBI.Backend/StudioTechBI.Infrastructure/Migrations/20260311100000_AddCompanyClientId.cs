using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_ClientId",
                table: "Companies",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Clients_ClientId",
                table: "Companies",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Clients_ClientId",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_ClientId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Companies");
        }
    }
}
