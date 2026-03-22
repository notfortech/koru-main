using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations;

/// <summary>
/// Legacy Tenants tables often predate BaseEntity soft-delete/audit columns. EF maps Tenant with
/// IsDeleted/CreatedBy/UpdatedBy; without them, admin tenant list fails with "Invalid column name 'IsDeleted'".
/// </summary>
[Migration("20260322120000_AddTenantsBaseEntityColumns")]
public partial class AddTenantsBaseEntityColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'IsDeleted')
            BEGIN
                ALTER TABLE [Tenants] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_Tenants_IsDeleted] DEFAULT (0);
            END
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'CreatedBy')
            BEGIN
                ALTER TABLE [Tenants] ADD [CreatedBy] nvarchar(max) NULL;
            END
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'UpdatedBy')
            BEGIN
                ALTER TABLE [Tenants] ADD [UpdatedBy] nvarchar(max) NULL;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'UpdatedBy')
                ALTER TABLE [Tenants] DROP COLUMN [UpdatedBy];
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'CreatedBy')
                ALTER TABLE [Tenants] DROP COLUMN [CreatedBy];
            IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Tenants_IsDeleted' AND parent_object_id = OBJECT_ID(N'[Tenants]'))
                ALTER TABLE [Tenants] DROP CONSTRAINT [DF_Tenants_IsDeleted];
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'IsDeleted')
                ALTER TABLE [Tenants] DROP COLUMN [IsDeleted];
            """);
    }
}
