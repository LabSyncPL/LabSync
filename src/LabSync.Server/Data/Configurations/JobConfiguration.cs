using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabSync.Server.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Command)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Arguments)
            .HasMaxLength(2000);

        builder.Property(j => j.ScriptPayload)
            .HasColumnType("text");

        builder.Property(j => j.Output)
            .HasMaxLength(50000);

        builder.HasOne(j => j.Device)
            .WithMany(d => d.Jobs)
            .HasForeignKey(j => j.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
