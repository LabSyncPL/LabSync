using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabSync.Modules.SystemInfo.Interfaces;
using LabSync.Modules.SystemInfo.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class DiskInfoService : IDiskInfoService
    {
        private readonly ILogger? _logger;

        public DiskInfoService(ILogger? logger)
        {
            _logger = logger;
        }

        public DiskInfo GetDiskInfo()
        {
            try
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .ToList();

                if (drives.Count == 0)
                {
                    return new DiskInfo { TotalGB = 0, FreeGB = 0, UsedGB = 0, UsagePercent = 0 };
                }

                var volumes = new List<DiskVolumeInfo>();
                double totalBytes = 0;
                double totalFreeBytes = 0;

                foreach (var drive in drives)
                {
                    var total = drive.TotalSize;
                    var free = drive.AvailableFreeSpace;
                    var used = total - free;

                    totalBytes += total;
                    totalFreeBytes += free;

                    volumes.Add(new DiskVolumeInfo
                    {
                        Name = drive.RootDirectory.FullName,
                        TotalGB = total / (1024.0 * 1024.0 * 1024.0),
                        FreeGB = free / (1024.0 * 1024.0 * 1024.0),
                        UsedGB = used / (1024.0 * 1024.0 * 1024.0),
                        UsagePercent = total > 0 ? Math.Round(used * 100.0 / total, 2) : 0.0
                    });
                }

                var usedBytes = totalBytes - totalFreeBytes;

                var primaryDrive = drives.FirstOrDefault(d =>
                    d.RootDirectory.FullName == "C:\\" || d.RootDirectory.FullName == "/") ?? drives.First();

                return new DiskInfo
                {
                    TotalGB = totalBytes / (1024.0 * 1024.0 * 1024.0),
                    FreeGB = totalFreeBytes / (1024.0 * 1024.0 * 1024.0),
                    UsedGB = usedBytes / (1024.0 * 1024.0 * 1024.0),
                    UsagePercent = totalBytes > 0 ? Math.Round(usedBytes * 100.0 / totalBytes, 2) : 0.0,
                    DriveName = primaryDrive.RootDirectory.FullName,
                    Volumes = volumes
                };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get disk info");
                return new DiskInfo { TotalGB = 0, FreeGB = 0, UsedGB = 0, UsagePercent = 0 };
            }
        }
    }
}
