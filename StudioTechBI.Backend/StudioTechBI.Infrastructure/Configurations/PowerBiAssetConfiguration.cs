using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class PowerBiAssetConfiguration : IEntityTypeConfiguration<PowerBiAsset>
{
    public void Configure(EntityTypeBuilder<PowerBiAsset> builder)
    {
        builder.ToTable("PowerBiAssets", "reporting");
        builder.HasKey(e => e.AssetId);

        builder.Property(e => e.WorkspaceId).HasMaxLength(100);
        builder.Property(e => e.DatasetId).HasMaxLength(100);
        builder.Property(e => e.ReportId).HasMaxLength(100);
        builder.Property(e => e.CapacityId).HasMaxLength(100);

        builder.Property(e => e.ReportType).HasMaxLength(100);

        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => new { e.ClientId, e.ReportType, e.IsActive });
    }
}

