using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class SchemaModelFieldAliasConfiguration : IEntityTypeConfiguration<SchemaModelFieldAlias>
{
    public void Configure(EntityTypeBuilder<SchemaModelFieldAlias> builder)
    {
        builder.ToTable("SchemaModelFieldAliases");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AliasName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.NormalizedAliasName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ObservedDataType).HasMaxLength(50);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ApprovalStatus).IsRequired().HasMaxLength(20);
        builder.Property(e => e.DecidedBy).HasMaxLength(256);

        builder.HasOne(e => e.SchemaModelField)
            .WithMany()
            .HasForeignKey(e => e.SchemaModelFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.FirstSeenReportMatchDraft)
            .WithMany()
            .HasForeignKey(e => e.FirstSeenReportMatchDraftId)
            .OnDelete(DeleteBehavior.SetNull);

        // One approved-or-not alias per (field, normalized name) — same alias text can map to
        // different fields on different models, deliberately not globally unique. Also backs
        // the upsert-by-alias lookup (bump ObservedCount instead of inserting a duplicate).
        builder.HasIndex(e => new { e.SchemaModelFieldId, e.NormalizedAliasName }).IsUnique();

        // Hot-path lookup: "which approved aliases match any of this client's normalized
        // column names" without needing to know the field up front — see ReportMatchService.
        builder.HasIndex(e => new { e.NormalizedAliasName, e.ApprovalStatus });

        builder.HasIndex(e => e.ApprovalStatus); // admin pending-review queue
    }
}
