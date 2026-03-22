using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioTechBI.Application.Services;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Data;

public static class DemoUsersSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var demoUsers = configuration.GetSection("DemoUsers").Get<List<DemoUserEntry>>();
        if (demoUsers == null || demoUsers.Count == 0)
            return;

        var roles = await context.Roles.Where(r => !r.IsDeleted).ToListAsync(cancellationToken);
        var roleByName = roles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in demoUsers)
        {
            var email = entry.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                continue;

            var exists = await context.Users.AnyAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);
            if (exists)
                continue;

            if (string.IsNullOrEmpty(entry.Password))
                continue;

            var roleName = entry.RoleName?.Trim() ?? "Client";
            if (!roleByName.TryGetValue(roleName, out var role))
                role = roles.FirstOrDefault(r => r.Name.Equals("Client", StringComparison.OrdinalIgnoreCase));
            if (role == null)
                continue;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = AuthService.HashPassword(entry.Password),
                FirstName = entry.FirstName?.Trim() ?? "User",
                LastName = entry.LastName?.Trim() ?? "Demo",
                PhoneNumber = entry.PhoneNumber?.Trim(),
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                CreatedBy = null,
                UpdatedBy = null
            };
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                CreatedBy = null,
                UpdatedBy = null,
                IsDeleted = false
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private class DemoUserEntry
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? RoleName { get; set; }
    }
}
