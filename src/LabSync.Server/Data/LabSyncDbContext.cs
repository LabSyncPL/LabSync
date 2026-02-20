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
            // Disable Lazy Loading for all entities in this context.
            // This forces explicit loading (e.g., via .Include()) and prevents N+1 problems.
            ChangeTracker.LazyLoadingEnabled = false;
        }

        public DbSet<Device> Devices { get; set; }
        public DbSet<Job> Jobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasIndex(e => e.MacAddress).IsUnique();
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