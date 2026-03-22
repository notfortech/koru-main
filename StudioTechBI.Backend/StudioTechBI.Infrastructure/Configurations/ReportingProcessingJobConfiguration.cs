using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ReportingProcessingJobConfiguration : IEntityTypeConfiguration<ReportingProcessingJob>
{
    public void Configure(EntityTypeBuilder<ReportingProcessingJob> builder)
    {
        builder.ToTable("ProcessingJobs", "reporting");
        builder.HasKey(e => e.JobId);
        builder.Property(e => e.FileName).HasMaxLength(500);
        builder.Property(e => e.BlobPath).HasMaxLength(1000);
        builder.Property(e => e.Status).HasMaxLength(100);
        builder.Property(e => e.CurrentStep).HasMaxLength(200);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
    }
}
