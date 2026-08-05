using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class CustomReportRequestConfiguration : IEntityTypeConfiguration<CustomReportRequest>
{
    public void Configure(EntityTypeBuilder<CustomReportRequest> builder)
    {
        builder.ToTable("CustomReportRequests");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
        builder.Property(e => e.RequestedByEmail).HasMaxLength(320);
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.SchemaSnapshotJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(e => e.SourceFileName).HasMaxLength(500);
        builder.Property(e => e.FulfilledByEmail).HasMaxLength(320);
        builder.Property(e => e.BlobPath).HasMaxLength(1000);
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => new { e.ClientId, e.Status })
            .HasFilter("[IsDeleted] = 0");
    }
}
