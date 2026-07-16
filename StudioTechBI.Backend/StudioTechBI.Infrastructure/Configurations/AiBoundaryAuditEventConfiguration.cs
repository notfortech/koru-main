using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class AiBoundaryAuditEventConfiguration : IEntityTypeConfiguration<AiBoundaryAuditEvent>
{
    public void Configure(EntityTypeBuilder<AiBoundaryAuditEvent> builder)
    {
        builder.ToTable("AiBoundaryAuditEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CorrelationId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Service).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Operation).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Phase).IsRequired().HasMaxLength(20);
        builder.Property(e => e.TargetService).IsRequired().HasMaxLength(100);
        builder.Property(e => e.MetadataJson).IsRequired();
        builder.Property(e => e.ErrorSummary).HasMaxLength(500);

        // Trace a single call end-to-end (Sent -> Received/Failed rows share one CorrelationId).
        builder.HasIndex(e => e.CorrelationId);

        // Admin queue default view: latest events, optionally filtered by operation.
        builder.HasIndex(e => new { e.Operation, e.CreatedAt });

        // "Prove nothing silently failed" queries.
        builder.HasIndex(e => e.Success);
    }
}
