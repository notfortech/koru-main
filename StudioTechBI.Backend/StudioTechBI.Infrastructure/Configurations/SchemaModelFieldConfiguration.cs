using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class SchemaModelFieldConfiguration : IEntityTypeConfiguration<SchemaModelField>
{
    public void Configure(EntityTypeBuilder<SchemaModelField> builder)
    {
        builder.ToTable("SchemaModelFields");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FieldName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.DataType).IsRequired().HasMaxLength(50);

        builder.HasIndex(e => new { e.SchemaModelId, e.FieldName }).IsUnique();
    }
}
