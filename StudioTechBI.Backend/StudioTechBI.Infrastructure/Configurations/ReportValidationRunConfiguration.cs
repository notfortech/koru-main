using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ReportValidationRunConfiguration : IEntityTypeConfiguration<ReportValidationRun>
{
    public void Configure(EntityTypeBuilder<ReportValidationRun> builder)
    {
        builder.ToTable("ReportValidationRuns");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.OverallResult).HasMaxLength(20);
        builder.Property(e => e.TemplateId).HasMaxLength(200);
        builder.Property(e => e.TemplateName).HasMaxLength(200);
        builder.Property(e => e.FiltersJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ReportSnapshotJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(e => e.SourceFileScratchBlobPath).HasMaxLength(500);
        builder.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => e.ClientId)
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Status)
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.NoAction);

        // Run <-> Checks relationship is configured from the Check side
        // (ReportValidationCheckConfiguration) to avoid declaring it twice.
    }
}
