using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("Templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TemplateName).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Industry).HasMaxLength(200);
        builder.Property(e => e.Version).IsRequired().HasMaxLength(50);
        builder.Property(e => e.BlobPath).HasMaxLength(1000);
        // SQL: uniqueidentifier (nullable) — not nvarchar(200)
        builder.Property(e => e.ModelId);
        builder.Property(e => e.RequiredColumnsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
        builder.Property(e => e.OptionalColumnsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
        builder.Property(e => e.CreatedDate).IsRequired();
    }
}
