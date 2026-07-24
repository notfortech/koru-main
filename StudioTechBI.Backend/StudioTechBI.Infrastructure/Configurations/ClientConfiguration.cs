using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ClientCode).HasMaxLength(50);
        builder.HasIndex(e => e.ClientCode).IsUnique().HasFilter("[ClientCode] IS NOT NULL");
        builder.Property(e => e.ClientName).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Industry).HasMaxLength(200);
        builder.Property(e => e.BlobFolderPath).HasMaxLength(1000);
        builder.Property(e => e.TemplateVersion).HasMaxLength(50);
        builder.Property(e => e.LogoBlobPath).HasMaxLength(500);
        builder.Property(e => e.CreatedDate).IsRequired();
        builder.HasMany(e => e.ProcessingJobs)
            .WithOne(j => j.Client)
            .HasForeignKey(j => j.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
