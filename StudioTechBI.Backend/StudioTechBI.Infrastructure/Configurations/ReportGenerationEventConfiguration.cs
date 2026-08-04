using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ReportGenerationEventConfiguration : IEntityTypeConfiguration<ReportGenerationEvent>
{
    public void Configure(EntityTypeBuilder<ReportGenerationEvent> builder)
    {
        builder.ToTable("ReportGenerationEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Mode).IsRequired().HasMaxLength(20);
        builder.Property(e => e.TemplateId).HasMaxLength(200);
        builder.Property(e => e.TemplateName).HasMaxLength(200);
        builder.Property(e => e.HtmlTemplateId).HasMaxLength(200);
        builder.Property(e => e.HtmlTemplateName).HasMaxLength(200);
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => new { e.ClientId, e.Mode })
            .HasFilter("[IsDeleted] = 0");
    }
}
