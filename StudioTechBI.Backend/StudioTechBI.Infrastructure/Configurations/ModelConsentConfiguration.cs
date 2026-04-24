using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class ModelConsentConfiguration : IEntityTypeConfiguration<ModelConsent>
{
    public void Configure(EntityTypeBuilder<ModelConsent> builder)
    {
        builder.ToTable("ModelConsents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SummaryJson).IsRequired();
        builder.Property(e => e.ApprovedAt).IsRequired();

        builder.HasIndex(e => e.ModelId).IsUnique();
        builder.HasIndex(e => e.ClientId);

        builder.HasOne(e => e.Model)
            .WithMany()
            .HasForeignKey(e => e.ModelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

