using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class OAuthConnectionStateConfiguration : IEntityTypeConfiguration<OAuthConnectionState>
{
    public void Configure(EntityTypeBuilder<OAuthConnectionState> builder)
    {
        builder.ToTable("OAuthConnectionStates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).IsRequired().HasMaxLength(50);
        builder.Property(x => x.State).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.HasIndex(x => x.State).IsUnique();
        builder.HasIndex(x => new { x.ClientId, x.Provider });
    }
}

