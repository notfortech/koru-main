using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class BlueprintConfiguration : IEntityTypeConfiguration<Blueprint>
{
    public void Configure(EntityTypeBuilder<Blueprint> builder)
    {
        builder.ToTable("Blueprints");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Industry).IsRequired().HasMaxLength(200);
        builder.Property(e => e.KnowledgePack).HasMaxLength(200);
        builder.Property(e => e.ProjectId).HasMaxLength(200);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => new { e.TenantId, e.ClientId, e.ProjectId })
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(e => e.Versions)
            .WithOne(v => v.Blueprint)
            .HasForeignKey(v => v.BlueprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Generations)
            .WithOne(g => g.Blueprint)
            .HasForeignKey(g => g.BlueprintId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
