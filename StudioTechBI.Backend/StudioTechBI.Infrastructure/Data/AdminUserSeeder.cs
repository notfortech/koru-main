using Microsoft.EntityFrameworkCore;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Enums;

namespace StudioTechBI.Infrastructure.Data;

public static class AdminUserSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.AdminUsers.AnyAsync())
            return;
        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Name = "System Admin",
            Email = "admin@studiotechbi.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123", workFactor: 11),
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
    }
}
