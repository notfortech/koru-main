using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class ReportMatchDraftConfiguration : IEntityTypeConfiguration<ReportMatchDraft>
{
    public void Configure(EntityTypeBuilder<ReportMatchDraft> builder)
    {
        builder.ToTable("ReportMatchDrafts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SchemaHash).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => e.Status);

        builder.HasOne(e => e.SchemaModel)
            .WithMany()
            .HasForeignKey(e => e.SchemaModelId)
            .OnDelete(DeleteBehavior.SetNull);

        // TemplateId is deliberately a plain column, not an EF-configured relationship — no
        // navigation property exists for it (mirrors how Template.ModelId, the unrelated legacy
        // column, is left unconfigured). The FK constraint itself still exists at the DB level.

        builder.HasMany(e => e.ColumnMappings)
            .WithOne(m => m.ReportMatchDraft)
            .HasForeignKey(m => m.ReportMatchDraftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
