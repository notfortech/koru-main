using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations;

/// <summary>0 = general client, 1 = accountant (client assignment). Column may already exist in some DBs.</summary>
[Migration("20260322143000_AddUserUserType")]
public partial class AddUserUserType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = N'UserType')
            BEGIN
                ALTER TABLE [Users] ADD [UserType] int NOT NULL CONSTRAINT [DF_Users_UserType] DEFAULT (0);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Users_UserType' AND parent_object_id = OBJECT_ID(N'[Users]'))
                ALTER TABLE [Users] DROP CONSTRAINT [DF_Users_UserType];
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = N'UserType')
                ALTER TABLE [Users] DROP COLUMN [UserType];
            """);
    }
}
