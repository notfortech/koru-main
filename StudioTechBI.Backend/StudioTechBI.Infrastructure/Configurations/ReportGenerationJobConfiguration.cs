using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ReportGenerationJobConfiguration : IEntityTypeConfiguration<ReportGenerationJob>
{
    public void Configure(EntityTypeBuilder<ReportGenerationJob> builder)
    {
        builder.ToTable("ReportGenerationJobs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.BlobPath).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.RequestPayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ResultJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        // Claimed via a conditional Pending->Processing update (see
        // ReportGenerationJobBackgroundService) -- indexed by Status so that scan is cheap even
        // as the table grows.
        builder.HasIndex(e => e.ClientId)
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Status)
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
