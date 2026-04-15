using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthConnectionStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OAuthConnectionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthConnectionStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnectionStates_ClientId_Provider",
                table: "OAuthConnectionStates",
                columns: new[] { "ClientId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthConnectionStates_State",
                table: "OAuthConnectionStates",
                column: "State",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuthConnectionStates");
        }
    }
}
