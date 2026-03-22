using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class FunctionalLogConfiguration : IEntityTypeConfiguration<FunctionalLog>
{
    public void Configure(EntityTypeBuilder<FunctionalLog> builder)
    {
        builder.ToTable("FunctionalLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Timestamp).IsRequired();
    }
}
