using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Enums;

namespace StudioTechBI.Infrastructure.Data;

public static class AdminUserSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IConfiguration configuration)
    {
        if (await db.AdminUsers.AnyAsync())
        {
            return;
        }

        var seedAdminEnabled = configuration.GetValue<bool>("SeedAdmin:Enabled");
        if (!seedAdminEnabled)
        {
            return;
        }

        var adminName = configuration["SeedAdmin:Name"];
        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminName) ||
            string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException("SeedAdmin configuration is incomplete.");
        }

        var adminRoleRaw = configuration["SeedAdmin:Role"] ?? AdminRole.SuperAdmin.ToString();
        if (!Enum.TryParse<AdminRole>(adminRoleRaw, true, out var adminRole))
        {
            throw new InvalidOperationException("SeedAdmin:Role is invalid.");
        }

        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Name = adminName.Trim(),
            Email = adminEmail.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 11),
            Role = adminRole,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
    }
}
