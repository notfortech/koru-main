using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class ReportDesignerConsentConfiguration : IEntityTypeConfiguration<ReportDesignerConsent>
{
    public void Configure(EntityTypeBuilder<ReportDesignerConsent> builder)
    {
        builder.ToTable("ReportDesignerConsents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SchemaHash).IsRequired().HasMaxLength(128);
        builder.Property(e => e.ApprovedAt).IsRequired();
        builder.Property(e => e.ApprovedBy).IsRequired().HasMaxLength(256);

        builder.HasIndex(e => new { e.ClientId, e.SchemaHash }).IsUnique();

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
