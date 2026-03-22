using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ReportingTechnicalLogConfiguration : IEntityTypeConfiguration<ReportingTechnicalLog>
{
    public void Configure(EntityTypeBuilder<ReportingTechnicalLog> builder)
    {
        builder.ToTable("TechnicalLogs", "reporting");
        builder.HasKey(e => e.LogId);
        builder.Property(e => e.Service).HasMaxLength(200);
        builder.Property(e => e.Level).HasMaxLength(50);
        builder.Property(e => e.Message).HasMaxLength(4000);
        builder.Property(e => e.StackTrace).HasMaxLength(8000);
    }
}
