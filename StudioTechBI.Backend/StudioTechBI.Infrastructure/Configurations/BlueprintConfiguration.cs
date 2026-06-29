using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class BlueprintConfiguration : IEntityTypeConfiguration<Blueprint>
{
    public void Configure(EntityTypeBuilder<Blueprint> builder)
    {
        builder.ToTable("Blueprints");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.BusinessRequirement).IsRequired().HasMaxLength(4000);
        builder.Property(e => e.Industry).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ExistingSchema).HasMaxLength(8000);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
        builder.Property(e => e.PdfDownloadUrl).HasMaxLength(2000);
        builder.Property(e => e.SubscriptionPlan).HasMaxLength(100);
        builder.Property(e => e.ResponseBlobPath).HasMaxLength(1000);
        builder.Property(e => e.RequestedByEmail).HasMaxLength(300);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => e.CreatedAt);

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
