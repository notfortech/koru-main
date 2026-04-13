using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class InsightModelConfiguration : IEntityTypeConfiguration<InsightModel>
{
    public void Configure(EntityTypeBuilder<InsightModel> builder)
    {
        builder.ToTable("InsightModels");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(100);
        builder.Property(e => e.MappingJson);
        builder.Property(e => e.ExcelSchemaJson);
        builder.HasIndex(e => e.ClientId);
        builder.HasOne(e => e.Client)
            .WithMany(c => c.InsightModels)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Datasets)
            .WithOne(d => d.Model)
            .HasForeignKey(d => d.ModelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
