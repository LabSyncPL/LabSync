using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabSync.Server.Data.Configurations;

public class ScheduledScriptConfiguration : IEntityTypeConfiguration<ScheduledScript>
{
    public void Configure(EntityTypeBuilder<ScheduledScript> builder)
    {
        builder.ToTable("ScheduledScripts");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.ScriptContent)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(s => s.Arguments)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(s => s.CronExpression)
            .HasMaxLength(100);

        builder.Property(s => s.CreatedBy)
            .HasMaxLength(200);

        builder.HasMany(s => s.Executions)
            .WithOne(e => e.ScheduledScript)
            .HasForeignKey(e => e.ScheduledScriptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
