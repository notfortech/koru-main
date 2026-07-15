using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class ReportDataUsageConsentConfiguration : IEntityTypeConfiguration<ReportDataUsageConsent>
{
    public void Configure(EntityTypeBuilder<ReportDataUsageConsent> builder)
    {
        builder.ToTable("ReportDataUsageConsents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ApprovedAt).IsRequired();
        builder.Property(e => e.ApprovedBy).IsRequired().HasMaxLength(256);

        // Deliberately no unique index — append-only, one row per Publish action.
        builder.HasIndex(e => e.ReportMatchDraftId);
        builder.HasIndex(e => e.ClientId);

        builder.HasOne(e => e.ReportMatchDraft)
            .WithMany()
            .HasForeignKey(e => e.ReportMatchDraftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
