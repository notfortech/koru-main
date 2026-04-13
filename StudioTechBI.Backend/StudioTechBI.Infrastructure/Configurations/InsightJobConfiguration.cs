using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class InsightJobConfiguration : IEntityTypeConfiguration<InsightJob>
{
    public void Configure(EntityTypeBuilder<InsightJob> builder)
    {
        builder.ToTable("InsightJobs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(4000);
        builder.HasIndex(e => e.ModelId);
    }
}
