using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class SavedReportVersionConfiguration : IEntityTypeConfiguration<SavedReportVersion>
{
    public void Configure(EntityTypeBuilder<SavedReportVersion> builder)
    {
        builder.ToTable("SavedReportVersions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.HtmlBlobPath).HasMaxLength(1000);
        builder.Property(e => e.TemplateId).HasMaxLength(200);
        builder.Property(e => e.TemplateName).HasMaxLength(200);
        builder.Property(e => e.HtmlTemplateId).HasMaxLength(200);
        builder.Property(e => e.HtmlTemplateName).HasMaxLength(200);
        builder.Property(e => e.SourceFileName).HasMaxLength(500);
        builder.Property(e => e.AppliedFiltersJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => new { e.SavedReportId, e.IsActive })
            .HasFilter("[IsDeleted] = 0");
    }
}
