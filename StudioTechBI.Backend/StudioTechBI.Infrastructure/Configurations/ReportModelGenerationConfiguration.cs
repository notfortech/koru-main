using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Configurations;

public class ReportModelGenerationConfiguration : IEntityTypeConfiguration<ReportModelGeneration>
{
    public void Configure(EntityTypeBuilder<ReportModelGeneration> builder)
    {
        builder.ToTable("ReportModelGenerations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RequestId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.RequestPayloadJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(e => e.ResponseJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
        builder.Property(e => e.CreatedBy).HasMaxLength(500);
        builder.Property(e => e.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(e => e.ClientId)
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Status)
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
