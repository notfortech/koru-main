using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.ValueConversion;

namespace StudioTechBI.Infrastructure.Configurations;

public class PowerBiAssetConfiguration : IEntityTypeConfiguration<PowerBiAsset>
{
    public void Configure(EntityTypeBuilder<PowerBiAsset> builder)
    {
        builder.ToTable("PowerBiAssets", "reporting");
        builder.HasKey(e => e.AssetId);

        // Power BI service IDs are commonly stored as uniqueidentifier in SQL, while the app uses strings
        // for easy passing to Power BI embed APIs.
        var guidString = GuidStringConverters.NullableStringToGuid();

        builder.Property(e => e.WorkspaceId)
            .HasConversion(guidString)
            .HasMaxLength(100);
        builder.Property(e => e.DatasetId)
            .HasConversion(guidString)
            .HasMaxLength(100);
        builder.Property(e => e.ReportId)
            .HasConversion(guidString)
            .HasMaxLength(100);
        builder.Property(e => e.CapacityId)
            .HasConversion(guidString)
            .HasMaxLength(100);
        // TemplateId can be a GUID, or a non-GUID string depending on the dataset row. Keep it as string in the model.
        builder.Property(e => e.TemplateId).HasMaxLength(100);

        builder.Property(e => e.ReportType).HasMaxLength(100);
        // SQL: bit NULL — not required in DB
        builder.Property(e => e.IsActive);

        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => new { e.ClientId, e.ReportType, e.IsActive });
    }
}

