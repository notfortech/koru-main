using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Description)
            .HasMaxLength(1000);

        builder.Property(o => o.TaxId)
            .HasMaxLength(50);

        builder.Property(o => o.Address)
            .HasMaxLength(500);

        builder.Property(o => o.City)
            .HasMaxLength(100);

        builder.Property(o => o.State)
            .HasMaxLength(100);

        builder.Property(o => o.ZipCode)
            .HasMaxLength(20);

        builder.Property(o => o.Country)
            .HasMaxLength(100);
    }
}
