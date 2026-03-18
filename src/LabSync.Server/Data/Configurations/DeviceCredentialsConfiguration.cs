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
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(dc => dc.SshKeyReference)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Ignore(dc => dc.SshPrivateKey);

        builder.Property(dc => dc.UseKeyAuthentication)
            .HasDefaultValue(true);

        builder.HasOne(dc => dc.Device)
            .WithOne(d => d.Credentials)
            .HasForeignKey<DeviceCredentials>(dc => dc.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
