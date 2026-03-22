using Microsoft.EntityFrameworkCore;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Data;

public static class RoleSeeder
{
    private static readonly string[] RoleNames = { "Admin", "Accountant", "Client" };

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Roles.AnyAsync(cancellationToken))
            return;

        var createdAt = DateTime.UtcNow;
        foreach (var name in RoleNames)
        {
            var exists = await context.Roles.AnyAsync(r => r.Name == name && !r.IsDeleted, cancellationToken);
            if (exists)
                continue;

            context.Roles.Add(new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = null,
                IsSystemRole = true,
                CreatedAt = createdAt,
                UpdatedAt = null,
                CreatedBy = null,
                UpdatedBy = null,
                IsDeleted = false
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
