using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class SchemaModelConfiguration : IEntityTypeConfiguration<SchemaModel>
{
    public void Configure(EntityTypeBuilder<SchemaModel> builder)
    {
        builder.ToTable("SchemaModels");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Industry).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.IsAiSuggested).IsRequired();
        builder.Property(e => e.ReviewStatus).IsRequired().HasMaxLength(20);
        builder.Property(e => e.SuggestedByClientId);

        builder.HasIndex(e => e.Industry);
        builder.HasIndex(e => e.ReviewStatus);
        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasMany(e => e.Fields)
            .WithOne(f => f.SchemaModel)
            .HasForeignKey(f => f.SchemaModelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
