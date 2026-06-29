using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class BlueprintVersionConfiguration : IEntityTypeConfiguration<BlueprintVersion>
{
    public void Configure(EntityTypeBuilder<BlueprintVersion> builder)
    {
        builder.ToTable("BlueprintVersions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PromptVersion).HasMaxLength(50);
        builder.Property(e => e.JsonBlobPath).HasMaxLength(1000);
        builder.Property(e => e.PdfBlobPath).HasMaxLength(1000);
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => new { e.BlueprintId, e.IsActive })
            .HasFilter("[IsDeleted] = 0");
    }
}
