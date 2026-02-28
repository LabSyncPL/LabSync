using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabSync.Server.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Hostname)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.MacAddress)
            .IsRequired()
            .HasMaxLength(17)
            .IsFixedLength();

        builder.Property(d => d.IpAddress)
            .HasMaxLength(45);

        builder.Property(d => d.OsVersion)
            .HasMaxLength(200);

        builder.Property(d => d.DeviceKeyHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasOne(d => d.Group)
            .WithMany(g => g.Devices)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.MacAddress).IsUnique();
    }
}
