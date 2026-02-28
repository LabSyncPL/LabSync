using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabSync.Server.Data.Configurations;

public class DeviceGroupConfiguration : IEntityTypeConfiguration<DeviceGroup>
{
    public void Configure(EntityTypeBuilder<DeviceGroup> builder)
    {
        builder.ToTable("DeviceGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.Description)
            .HasMaxLength(500);

        builder.Navigation(g => g.Devices)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
