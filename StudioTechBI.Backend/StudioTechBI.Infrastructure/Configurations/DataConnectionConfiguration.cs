using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class DataConnectionConfiguration : IEntityTypeConfiguration<DataConnection>
{
    public void Configure(EntityTypeBuilder<DataConnection> builder)
    {
        builder.ToTable("DataConnections");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).IsRequired().HasMaxLength(50);
        builder.Property(e => e.AccessToken);
        builder.Property(e => e.RefreshToken);
        builder.Property(e => e.ConfigJson);
        builder.HasIndex(e => e.ClientId);
        builder.HasOne(e => e.Client)
            .WithMany(c => c.DataConnections)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
