using Microsoft.EntityFrameworkCore;
using StudioTechBI.Domain.Entities;
using System.Reflection;

namespace StudioTechBI.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<RegistrationAttempt> RegistrationAttempts => Set<RegistrationAttempt>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<PowerBiAsset> PowerBiAssets => Set<PowerBiAsset>();
    public DbSet<FunctionalLog> FunctionalLogs => Set<FunctionalLog>();
    public DbSet<TechnicalLog> TechnicalLogs => Set<TechnicalLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DatasetRefreshLog> DatasetRefreshLogs => Set<DatasetRefreshLog>();
    public DbSet<ReportingTechnicalLog> ReportingTechnicalLogs => Set<ReportingTechnicalLog>();
    public DbSet<ReportingProcessingJob> ReportingProcessingJobs => Set<ReportingProcessingJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
