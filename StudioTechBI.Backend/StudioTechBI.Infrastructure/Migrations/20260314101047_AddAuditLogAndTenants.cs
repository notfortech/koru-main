using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioTechBI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogAndTenants : Migration
    {
        /// <inheritdoc />
        /// <remarks>AuditLogs and Tenants tables already exist in the database; this migration only ensures the migration is recorded in __EFMigrationsHistory.</remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tables already exist; no CREATE TABLE. EF Core will insert this migration into __EFMigrationsHistory when Up() completes.
        }

        /// <inheritdoc />
        /// <remarks>Tables are kept in place; no DROP TABLE.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tables remain in database; no DROP TABLE.
        }
    }
}
