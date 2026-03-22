using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class DatasetRefreshLogConfiguration : IEntityTypeConfiguration<DatasetRefreshLog>
{
    public void Configure(EntityTypeBuilder<DatasetRefreshLog> builder)
    {
        builder.ToTable("DatasetRefreshLogs", "reporting");
        builder.HasKey(e => e.RefreshId);
        builder.Property(e => e.DatasetName).HasMaxLength(500);
        builder.Property(e => e.Status).HasMaxLength(100);
        builder.Property(e => e.ErrorMessage).HasMaxLength(4000);
    }
}
