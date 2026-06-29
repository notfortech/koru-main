using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class BlueprintGenerationConfiguration : IEntityTypeConfiguration<BlueprintGeneration>
{
    public void Configure(EntityTypeBuilder<BlueprintGeneration> builder)
    {
        builder.ToTable("BlueprintGenerations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RequestId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
        builder.Property(e => e.RequestPayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Warnings).HasMaxLength(2000);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => e.Status)
            .HasFilter("[IsDeleted] = 0");

        // Optional link back to the created version
        builder.HasOne(e => e.BlueprintVersion)
            .WithMany()
            .HasForeignKey(e => e.BlueprintVersionId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
