using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabSync.Server.Data.Configurations;

public class ScheduledScriptExecutionConfiguration : IEntityTypeConfiguration<ScheduledScriptExecution>
{
    public void Configure(EntityTypeBuilder<ScheduledScriptExecution> builder)
    {
        builder.ToTable("ScheduledScriptExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Error)
            .HasMaxLength(2000);

        builder.HasOne(e => e.ScheduledScript)
            .WithMany(s => s.Executions)
            .HasForeignKey(e => e.ScheduledScriptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
