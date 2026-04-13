using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioTechBI.Infrastructure.Data;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260331120000_AddUserPasswordResetFields")]
public partial class AddUserPasswordResetFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PasswordResetTokenHash",
            table: "Users",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PasswordResetTokenExpiry",
            table: "Users",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PasswordResetTokenHash",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "PasswordResetTokenExpiry",
            table: "Users");
    }
}
