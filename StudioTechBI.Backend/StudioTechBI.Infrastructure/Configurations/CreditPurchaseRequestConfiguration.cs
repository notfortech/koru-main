using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class CreditPurchaseRequestConfiguration : IEntityTypeConfiguration<CreditPurchaseRequest>
{
    public void Configure(EntityTypeBuilder<CreditPurchaseRequest> builder)
    {
        builder.ToTable("CreditPurchaseRequests");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RequestedByEmail).HasMaxLength(320);
        builder.Property(e => e.PackLabel).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.PaidByEmail).HasMaxLength(320);
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => new { e.ClientId, e.Status })
            .HasFilter("[IsDeleted] = 0");
    }
}
