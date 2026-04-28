using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class ReportingReportRequestConfiguration : IEntityTypeConfiguration<ReportingReportRequest>
{
    public void Configure(EntityTypeBuilder<ReportingReportRequest> builder)
    {
        builder.ToTable("ReportRequests", "reporting");
        builder.HasKey(e => e.RequestId);

        builder.Property(e => e.SourceBlobPath).HasMaxLength(2000);
        builder.Property(e => e.Status).HasMaxLength(50).IsRequired();
        builder.Property(e => e.RequestedByEmail).HasMaxLength(320);

        builder.Property(e => e.CreatedAtUtc).IsRequired();
    }
}

