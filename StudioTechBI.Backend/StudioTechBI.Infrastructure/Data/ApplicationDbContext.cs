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
    public DbSet<ClientBlobFolder> ClientBlobFolders => Set<ClientBlobFolder>();
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
    public DbSet<ReportingReportRequest> ReportingReportRequests => Set<ReportingReportRequest>();
    public DbSet<InsightModel> InsightModels => Set<InsightModel>();
    public DbSet<InsightDataset> InsightDatasets => Set<InsightDataset>();
    public DbSet<InsightJob> InsightJobs => Set<InsightJob>();
    public DbSet<ModelConsent> ModelConsents => Set<ModelConsent>();
    public DbSet<ReportDesignerConsent> ReportDesignerConsents => Set<ReportDesignerConsent>();
    public DbSet<SchemaModel> SchemaModels => Set<SchemaModel>();
    public DbSet<SchemaModelField> SchemaModelFields => Set<SchemaModelField>();
    public DbSet<SchemaModelFieldAlias> SchemaModelFieldAliases => Set<SchemaModelFieldAlias>();
    public DbSet<AiBoundaryAuditEvent> AiBoundaryAuditEvents => Set<AiBoundaryAuditEvent>();
    public DbSet<ReportMatchDraft> ReportMatchDrafts => Set<ReportMatchDraft>();
    public DbSet<ReportMatchColumnMapping> ReportMatchColumnMappings => Set<ReportMatchColumnMapping>();
    public DbSet<ReportDataUsageConsent> ReportDataUsageConsents => Set<ReportDataUsageConsent>();
    public DbSet<DataConnection> DataConnections => Set<DataConnection>();
    public DbSet<OAuthConnectionState> OAuthConnectionStates => Set<OAuthConnectionState>();

    // AI Gateway — Blueprint module
    public DbSet<Blueprint> Blueprints => Set<Blueprint>();
    public DbSet<BlueprintVersion> BlueprintVersions => Set<BlueprintVersion>();
    public DbSet<BlueprintGeneration> BlueprintGenerations => Set<BlueprintGeneration>();

    // Report Validation module
    public DbSet<ReportValidationRun> ReportValidationRuns => Set<ReportValidationRun>();
    public DbSet<ReportValidationCheck> ReportValidationChecks => Set<ReportValidationCheck>();

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
