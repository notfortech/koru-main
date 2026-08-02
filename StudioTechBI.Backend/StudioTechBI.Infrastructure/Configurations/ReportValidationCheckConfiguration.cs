using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ReportValidationCheckConfiguration : IEntityTypeConfiguration<ReportValidationCheck>
{
    public void Configure(EntityTypeBuilder<ReportValidationCheck> builder)
    {
        builder.ToTable("ReportValidationChecks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CheckFamily).IsRequired().HasMaxLength(30);
        builder.Property(e => e.CheckName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Detail).HasMaxLength(2000);
        builder.Property(e => e.EvidenceJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => e.ReportValidationRunId)
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.Run)
            .WithMany(r => r.Checks)
            .HasForeignKey(e => e.ReportValidationRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
