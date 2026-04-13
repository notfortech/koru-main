using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class InsightDatasetConfiguration : IEntityTypeConfiguration<InsightDataset>
{
    public void Configure(EntityTypeBuilder<InsightDataset> builder)
    {
        builder.ToTable("InsightDatasets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PowerBIDatasetId).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ReportId).IsRequired().HasMaxLength(200);
        builder.HasIndex(e => e.ModelId);
    }
}
