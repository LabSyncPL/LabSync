using System;
using System.Runtime.InteropServices;
using LabSync.Modules.SystemInfo.Interfaces;
using LabSync.Modules.SystemInfo.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class MemoryInfoService : IMemoryInfoService
    {
        private readonly ILogger? _logger;
        private readonly bool _isWindows;

        public MemoryInfoService(ILogger? logger)
        {
            _logger = logger;
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public MemoryInfo GetMemoryInfo()
        {
            try
            {
                return _isWindows ? GetWindowsMemoryInfo() : GetLinuxMemoryInfo();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get memory info");
                return new MemoryInfo { TotalMB = 0, AvailableMB = 0, UsedMB = 0, UsagePercent = 0 };
            }
        }

        private MemoryInfo GetWindowsMemoryInfo()
        {
            var memStatus = new WindowsMemoryStatus();
            WindowsMemoryStatus.GlobalMemoryStatusEx(memStatus);

            var totalBytes = (long)memStatus.TotalPhys;
            var availableBytes = (long)memStatus.AvailPhys;
            var usedBytes = totalBytes - availableBytes;

            return new MemoryInfo
            {
                TotalMB = totalBytes / (1024 * 1024),
                AvailableMB = availableBytes / (1024 * 1024),
                UsedMB = usedBytes / (1024 * 1024),
                UsagePercent = totalBytes > 0 ? Math.Round((double)usedBytes / totalBytes * 100, 2) : 0
            };
        }

        private MemoryInfo GetLinuxMemoryInfo()
        {
            try
            {
                var memInfo = File.ReadAllText("/proc/meminfo");
                long totalKB = 0, availableKB = 0, freeKB = 0, buffersKB = 0, cachedKB = 0;

                foreach (var line in memInfo.Split('\n'))
                {
                    if (line.StartsWith("MemTotal:"))
                        totalKB = long.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    else if (line.StartsWith("MemAvailable:"))
                        availableKB = long.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    else if (line.StartsWith("MemFree:"))
                        freeKB = long.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    else if (line.StartsWith("Buffers:"))
                        buffersKB = long.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    else if (line.StartsWith("Cached:"))
                        cachedKB = long.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                }

                if (availableKB == 0)
                {
                    availableKB = freeKB + buffersKB + cachedKB;
                }

                var totalMB = totalKB / 1024;
                var availableMB = availableKB / 1024;
                var usedMB = totalMB - availableMB;

                return new MemoryInfo
                {
                    TotalMB = totalMB,
                    AvailableMB = availableMB,
                    UsedMB = usedMB,
                    UsagePercent = totalMB > 0 ? Math.Round((double)usedMB / totalMB * 100, 2) : 0
                };
            }
            catch
            {
                return new MemoryInfo { TotalMB = 0, AvailableMB = 0, UsedMB = 0, UsagePercent = 0 };
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class WindowsMemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;

            public WindowsMemoryStatus()
            {
                Length = (uint)Marshal.SizeOf(typeof(WindowsMemoryStatus));
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GlobalMemoryStatusEx(WindowsMemoryStatus buffer);
        }
    }
}

