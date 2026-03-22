using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).IsRequired().HasMaxLength(200);
        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(e => e.EntityId).HasMaxLength(100);
        builder.Property(e => e.OldValue).HasMaxLength(8000);
        builder.Property(e => e.NewValue).HasMaxLength(8000);
        builder.Property(e => e.IpAddress).HasMaxLength(100);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.Timestamp).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Timestamp });
        builder.HasIndex(e => new { e.UserId, e.Timestamp });
    }
}
