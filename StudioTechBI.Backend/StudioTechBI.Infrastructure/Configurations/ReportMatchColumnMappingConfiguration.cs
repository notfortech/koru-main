using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class ReportMatchColumnMappingConfiguration : IEntityTypeConfiguration<ReportMatchColumnMapping>
{
    public void Configure(EntityTypeBuilder<ReportMatchColumnMapping> builder)
    {
        builder.ToTable("ReportMatchColumnMappings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FieldName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.DataType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ClientColumnName).HasMaxLength(200);

        builder.HasIndex(e => e.ReportMatchDraftId);
    }
}
