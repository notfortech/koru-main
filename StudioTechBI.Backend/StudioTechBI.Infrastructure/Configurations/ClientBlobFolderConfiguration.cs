using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public sealed class ClientBlobFolderConfiguration : IEntityTypeConfiguration<ClientBlobFolder>
{
    public void Configure(EntityTypeBuilder<ClientBlobFolder> builder)
    {
        builder.ToTable("ClientBlobFolders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FolderPath).IsRequired().HasMaxLength(500);
        builder.HasIndex(e => e.ClientId).IsUnique();
        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
