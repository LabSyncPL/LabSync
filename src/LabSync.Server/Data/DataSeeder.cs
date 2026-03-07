using LabSync.Core.Entities;
using LabSync.Core.Types;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(LabSyncDbContext context)
    {
        context.AgentLogs.RemoveRange(context.AgentLogs);
        context.Jobs.RemoveRange(context.Jobs);
        context.Devices.RemoveRange(context.Devices);
        context.AdminUsers.RemoveRange(context.AdminUsers);

        await context.SaveChangesAsync();

        // -------------------------
        // ADMIN
        // -------------------------
        //var admin = new AdminUser("admin", "DEMO_HASH");
        //context.AdminUsers.Add(admin);

        // -------------------------
        // DEVICE GROUPS
        // -------------------------
        var windowsGroup = new DeviceGroup("Windows Machines", "Production Windows devices");
        var linuxGroup = new DeviceGroup("Linux Servers", "Backend servers");

        context.AddRange(windowsGroup, linuxGroup);

        // -------------------------
        // DEVICES
        // -------------------------
        var devices = new List<Device>();

        for (int i = 1; i <= 5; i++)
        {
            var device = new Device(
                $"WIN-PC-{i}",
                $"AA-BB-CC-DD-EE-{i:00}",
                DevicePlatform.Windows,
                "Windows 11 Pro",
                Guid.NewGuid().ToString()
            );

            device.Approve();
            device.RecordHeartbeat($"192.168.1.{i}");

            windowsGroup.AddDevice(device);
            devices.Add(device);
        }

        for (int i = 1; i <= 3; i++)
        {
            var device = new Device(
                $"LINUX-SRV-{i}",
                $"FF-EE-DD-CC-BB-{i:00}",
                DevicePlatform.Linux,
                "Ubuntu 22.04",
                Guid.NewGuid().ToString()
            );

            device.Approve();
            device.RecordHeartbeat($"10.0.0.{i}");

            linuxGroup.AddDevice(device);
            devices.Add(device);
        }

        context.Devices.AddRange(devices);

        // -------------------------
        // JOBS
        // -------------------------
        var jobs = new List<Job>();

        foreach (var device in devices)
        {
            var job1 = new Job(device.Id, "ipconfig");
            job1.MarkAsRunning();
            job1.Complete(0, "OK");

            var job2 = new Job(device.Id, "cleanup-temp");

            jobs.Add(job1);
            jobs.Add(job2);
        }

        context.Jobs.AddRange(jobs);

        await context.SaveChangesAsync();

        // -------------------------
        // AGENT LOGS
        // -------------------------
        var logs = new List<AgentLog>();
        var random = new Random();

        foreach (var device in devices)
        {
            var startTime = DateTime.UtcNow.AddMinutes(-5);

            for (int i = 0; i < 50; i++) // 50 logs per device
            {
                var timestamp = startTime.AddSeconds(i * 6);

                logs.Add(new AgentLog(
                    device.Id,
                    timestamp,
                    random.NextDouble() * 100,
                    random.Next(1000, 16000),
                    random.Next(0, 20) == 0 ? "High CPU usage" : null
                ));
            }
        }

        await context.AgentLogs.AddRangeAsync(logs);
        await context.SaveChangesAsync();
    }
}