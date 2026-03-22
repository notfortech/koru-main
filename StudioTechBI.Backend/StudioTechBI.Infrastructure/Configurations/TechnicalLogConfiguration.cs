using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class TechnicalLogConfiguration : IEntityTypeConfiguration<TechnicalLog>
{
    public void Configure(EntityTypeBuilder<TechnicalLog> builder)
    {
        builder.ToTable("TechnicalLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Service).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Level).IsRequired();
        builder.Property(e => e.Message).IsRequired().HasMaxLength(4000);
        builder.Property(e => e.StackTrace).HasMaxLength(8000);
        builder.Property(e => e.Timestamp).IsRequired();
    }
}
