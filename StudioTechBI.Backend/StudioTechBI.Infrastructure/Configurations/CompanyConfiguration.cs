using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.ABN)
            .HasMaxLength(50);

        builder.Property(c => c.Industry)
            .HasMaxLength(100);

        builder.Property(c => c.Country)
            .HasMaxLength(2);

        builder.HasOne(c => c.Client)
            .WithMany()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasMany(c => c.CompanyUsers)
            .WithOne(cu => cu.Company)
            .HasForeignKey(cu => cu.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.BankConnections)
            .WithOne(bc => bc.Company)
            .HasForeignKey(bc => bc.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.BankTransactions)
            .WithOne(bt => bt.Company)
            .HasForeignKey(bt => bt.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
