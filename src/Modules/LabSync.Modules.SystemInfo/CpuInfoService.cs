using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using LabSync.Modules.SystemInfo.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class CpuInfoService : ICpuInfoService
    {
        private readonly ILogger? _logger;
        private readonly bool _isWindows;
        private DateTime _lastSampleTime = DateTime.MinValue;
        private double _lastValue;

        public CpuInfoService(ILogger? logger)
        {
            _logger = logger;
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public double GetCpuUsage()
        {
            try
            {
                var now = DateTime.UtcNow;
                if ((now - _lastSampleTime).TotalMilliseconds < 500)
                {
                    return _lastValue;
                }

                var value = _isWindows ? GetWindowsCpuUsage() : GetLinuxCpuUsage();
                _lastSampleTime = now;
                _lastValue = value;
                return value;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get CPU usage");
                return _lastValue;
            }
        }

        private double GetWindowsCpuUsage()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpu = process.TotalProcessorTime;

                Thread.Sleep(500);

                process.Refresh();
                var endTime = DateTime.UtcNow;
                var endCpu = process.TotalProcessorTime;

                var cpuUsedMs = (endCpu - startCpu).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed) * 100;

                return Math.Round(Math.Min(100.0, Math.Max(0.0, cpuUsageTotal)), 2);
            }
            catch
            {
                return _lastValue;
            }
        }

        private double GetLinuxCpuUsage()
        {
            try
            {
                if (!File.Exists("/proc/stat")) return 0.0;

                var line = File.ReadAllLines("/proc/stat").FirstOrDefault();
                if (string.IsNullOrEmpty(line)) return 0.0;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 8) return 0.0;

                var user = long.Parse(parts[1]);
                var nice = long.Parse(parts[2]);
                var system = long.Parse(parts[3]);
                var idle = long.Parse(parts[4]);
                var iowait = long.Parse(parts[5]);
                var irq = long.Parse(parts[6]);
                var softirq = long.Parse(parts[7]);

                var totalIdle = idle + iowait;
                var totalNonIdle = user + nice + system + irq + softirq;
                var total = totalIdle + totalNonIdle;

                Thread.Sleep(1000);

                var line2 = File.ReadAllLines("/proc/stat").FirstOrDefault();
                if (string.IsNullOrEmpty(line2)) return 0.0;

                var parts2 = line2.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts2.Length < 8) return 0.0;

                var user2 = long.Parse(parts2[1]);
                var nice2 = long.Parse(parts2[2]);
                var system2 = long.Parse(parts2[3]);
                var idle2 = long.Parse(parts2[4]);
                var iowait2 = long.Parse(parts2[5]);
                var irq2 = long.Parse(parts2[6]);
                var softirq2 = long.Parse(parts2[7]);

                var totalIdle2 = idle2 + iowait2;
                var totalNonIdle2 = user2 + nice2 + system2 + irq2 + softirq2;
                var total2 = totalIdle2 + totalNonIdle2;

                var totalIdleDiff = totalIdle2 - totalIdle;
                var totalDiff = total2 - total;

                if (totalDiff == 0) return 0.0;

                var cpuPercent = 100.0 * (totalDiff - totalIdleDiff) / totalDiff;
                return Math.Round(Math.Max(0.0, Math.Min(100.0, cpuPercent)), 2);
            }
            catch
            {
                return 0.0;
            }
        }
    }
}

