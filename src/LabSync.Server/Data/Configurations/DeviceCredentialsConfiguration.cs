using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabSync.Server.Data.Configurations;

public class DeviceCredentialsConfiguration : IEntityTypeConfiguration<DeviceCredentials>
{
    public void Configure(EntityTypeBuilder<DeviceCredentials> builder)
    {
        builder.HasKey(dc => dc.Id);

        builder.Property(dc => dc.SshUsername)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(dc => dc.SshPassword)
            .HasMaxLength(500);

        builder.HasOne(dc => dc.Device)
            .WithOne(d => d.Credentials)
            .HasForeignKey<DeviceCredentials>(dc => dc.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
