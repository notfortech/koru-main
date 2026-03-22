using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> builder)
    {
        builder.ToTable("BankConnections");

        builder.HasKey(bc => bc.Id);

        builder.Property(bc => bc.CompanyId)
            .IsRequired();

        builder.Property(bc => bc.ProviderName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(bc => bc.ConnectionId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(bc => bc.Status)
            .IsRequired();

        builder.HasOne(bc => bc.Company)
            .WithMany(c => c.BankConnections)
            .HasForeignKey(bc => bc.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(bc => bc.BankTransactions)
            .WithOne(bt => bt.BankConnection)
            .HasForeignKey(bt => bt.BankConnectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(bc => new { bc.CompanyId, bc.ProviderName });
    }
}
