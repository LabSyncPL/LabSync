using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Data;

public class LabSyncDbContext : DbContext
{
    public LabSyncDbContext(DbContextOptions<LabSyncDbContext> options) : base(options)
    {
        ChangeTracker.LazyLoadingEnabled = false;
    }

    public DbSet<Device> Devices { get; set; }
    public DbSet<DeviceCredentials> DeviceCredentials { get; set; }
    public DbSet<Job> Jobs { get; set; }
    public DbSet<AdminUser> AdminUsers { get; set; }
    

    public DbSet<AgentLog> AgentLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<AgentLog>()
            .HasKey(l => new { l.DeviceId, l.Timestamp });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LabSyncDbContext).Assembly);
    }
}