using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("BankTransactions");

        builder.HasKey(bt => bt.Id);

        builder.Property(bt => bt.CompanyId)
            .IsRequired();

        builder.Property(bt => bt.Amount)
            .HasPrecision(19, 2)
            .IsRequired();

        builder.Property(bt => bt.Description)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(bt => bt.TransactionDate)
            .IsRequired();

        builder.HasOne(bt => bt.Company)
            .WithMany(c => c.BankTransactions)
            .HasForeignKey(bt => bt.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(bt => bt.BankConnection)
            .WithMany(bc => bc.BankTransactions)
            .HasForeignKey(bt => bt.BankConnectionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(bt => new { bt.CompanyId, bt.TransactionDate });
        builder.HasIndex(bt => bt.BankConnectionId);
    }
}
