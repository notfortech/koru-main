using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class PowerBiAssetConfiguration : IEntityTypeConfiguration<PowerBiAsset>
{
    public void Configure(EntityTypeBuilder<PowerBiAsset> builder)
    {
        builder.ToTable("PowerBiAssets", "reporting");
        builder.HasKey(e => e.AssetId);

        // Some environments store these ids as uniqueidentifier in SQL.
        // Use a converter so the app can keep them as strings (used in embeds / UI) without schema changes.
        var guidToString = new ValueConverter<string?, Guid?>(
            v => string.IsNullOrWhiteSpace(v) ? null : Guid.Parse(v),
            v => v.HasValue ? v.Value.ToString() : null);

        builder.Property(e => e.WorkspaceId)
            .HasConversion(guidToString)
            .HasMaxLength(100);
        builder.Property(e => e.DatasetId)
            .HasConversion(guidToString)
            .HasMaxLength(100);
        builder.Property(e => e.ReportId)
            .HasConversion(guidToString)
            .HasMaxLength(100);
        builder.Property(e => e.CapacityId)
            .HasConversion(guidToString)
            .HasMaxLength(100);

        builder.Property(e => e.ReportType).HasMaxLength(100);

        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => new { e.ClientId, e.ReportType, e.IsActive });
    }
}

