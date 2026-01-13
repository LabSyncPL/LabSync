using LabSync.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace LabSync.Server.Data
{
    public class LabSyncDbContext : DbContext
    {
        public LabSyncDbContext(DbContextOptions<LabSyncDbContext> options) : base(options)
        {
        }

        public DbSet<Device> Devices { get; set; }
        public DbSet<Job> Jobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasIndex(e => e.MacAddress).IsUnique();
                entity.HasIndex(e => e.AgentToken);
            });

            modelBuilder.Entity<Job>(entity =>
            {
                entity.HasOne(j => j.Device)
                      .WithMany(d => d.Jobs)
                      .HasForeignKey(j => j.DeviceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}