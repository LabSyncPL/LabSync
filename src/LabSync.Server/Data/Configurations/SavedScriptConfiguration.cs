using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabSync.Server.Data.Configurations;

public class SavedScriptConfiguration : IEntityTypeConfiguration<SavedScript>
{
    public void Configure(EntityTypeBuilder<SavedScript> builder)
    {
        builder.ToTable("SavedScripts");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(1_000);

        builder.Property(s => s.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(s => s.Interpreter)
            .IsRequired()
            .HasMaxLength(20);
    }
}
