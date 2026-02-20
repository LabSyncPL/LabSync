using LabSync.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LabSync.Modules.SystemInfo
{
    /// <summary>
    /// System information and metrics collection module.
    /// Collects real-time system metrics including CPU, memory, disk usage, and system details.
    /// Cross-platform support for Windows and Linux.
    /// </summary>
    public class SystemInfoModule : IAgentModule
    {
        public string Name => "SystemInfo";
        public string Version => "2.0.0";

        private ILogger? _logger;
        private readonly bool _isWindows;
        private DateTime _lastCpuCheck = DateTime.MinValue;
        private double _lastCpuValue = 0.0;

        public SystemInfoModule()
        {
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var loggerFactory = (ILoggerFactory?)serviceProvider.GetService(typeof(ILoggerFactory));
            _logger = loggerFactory?.CreateLogger<SystemInfoModule>();

            _logger?.LogInformation("SystemInfo module initialized. Platform: {Platform}", 
                RuntimeInformation.OSDescription);
            
            return Task.CompletedTask;
        }

        public bool CanHandle(string jobType)
        {
            return jobType.Equals("CollectMetrics", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("Get-SysInfo", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("SystemInfo", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Collecting system metrics...");

                var metrics = await CollectSystemMetricsAsync(cancellationToken);

                _logger?.LogDebug("Successfully collected system metrics. CPU: {Cpu}%, Memory: {Memory}%", 
                    metrics.CpuLoad, metrics.MemoryInfo.UsagePercent);

                return ModuleResult.Success(metrics);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("System metrics collection was cancelled.");
                return ModuleResult.Failure("Operation was cancelled.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error collecting system metrics");
                return ModuleResult.Failure($"Failed to collect metrics: {ex.Message}");
            }
        }

        private async Task<SystemMetrics> CollectSystemMetricsAsync(CancellationToken cancellationToken)
        {
            // Run collection in parallel for better performance
            var cpuTask = Task.Run(() => GetCpuUsage(), cancellationToken);
            var memoryTask = Task.Run(() => GetMemoryInfo(), cancellationToken);
            var diskTask = Task.Run(() => GetDiskInfo(), cancellationToken);
            var systemInfoTask = Task.Run(() => GetSystemInfo(), cancellationToken);

            await Task.WhenAll(cpuTask, memoryTask, diskTask, systemInfoTask);

            return new SystemMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuLoad = await cpuTask,
                MemoryInfo = await memoryTask,
                DiskInfo = await diskTask,
                SystemInfo = await systemInfoTask
            };
        }

        private double GetCpuUsage()
        {
            try
            {
                if (_isWindows)
                {
                    return GetWindowsCpuUsage();
                }
                else
                {
                    // Linux: Read from /proc/stat
                    return GetLinuxCpuUsage();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get CPU usage");
                return 0.0;
            }
        }

        private double GetWindowsCpuUsage()
        {
            try
            {
                // Use WMI-like approach via PerformanceCounter (if available) or fallback to simpler method
                // For .NET 9, we'll use a simpler approach reading from WMI or performance data
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
                
                return Math.Round(Math.Min(100.0, cpuUsageTotal), 2);
            }
            catch
            {
                // Fallback: return cached value or 0
                return _lastCpuValue;
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

                // Parse CPU times
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

                // Wait a bit and read again
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
                return Math.Round(cpuPercent, 2);
            }
            catch
            {
                return 0.0;
            }
        }

        private MemoryInfo GetMemoryInfo()
        {
            try
            {
                if (_isWindows)
                {
                    return GetWindowsMemoryInfo();
                }
                else
                {
                    return GetLinuxMemoryInfo();
                }
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

                // If MemAvailable is not available (older kernels), calculate it
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

        private DiskInfo GetDiskInfo()
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

                // Get the primary drive (C: on Windows, / on Linux)
                var primaryDrive = drives.FirstOrDefault(d => 
                    d.RootDirectory.FullName == "C:\\" || d.RootDirectory.FullName == "/") 
                    ?? drives.First();

                var totalBytes = primaryDrive.TotalSize;
                var freeBytes = primaryDrive.AvailableFreeSpace;
                var usedBytes = totalBytes - freeBytes;

                return new DiskInfo
                {
                    TotalGB = totalBytes / (1024.0 * 1024.0 * 1024.0),
                    FreeGB = freeBytes / (1024.0 * 1024.0 * 1024.0),
                    UsedGB = usedBytes / (1024.0 * 1024.0 * 1024.0),
                    UsagePercent = totalBytes > 0 ? Math.Round((double)usedBytes / totalBytes * 100, 2) : 0,
                    DriveName = primaryDrive.RootDirectory.FullName
                };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get disk info");
                return new DiskInfo { TotalGB = 0, FreeGB = 0, UsedGB = 0, UsagePercent = 0 };
            }
        }

        private SystemDetails GetSystemInfo()
        {
            return new SystemDetails
            {
                OSPlatform = _isWindows ? "Windows" : "Linux",
                OSDescription = RuntimeInformation.OSDescription,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                Uptime = GetSystemUptime()
            };
        }

        private string GetSystemUptime()
        {
            try
            {
                if (_isWindows)
                {
                    using var uptime = Process.Start(new ProcessStartInfo
                    {
                        FileName = "net",
                        Arguments = "stats srv",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    // Windows uptime is complex, return a placeholder for now
                    return "N/A";
                }
                else
                {
                    var uptimeSeconds = long.Parse(File.ReadAllText("/proc/uptime").Split(' ')[0]);
                    var uptime = TimeSpan.FromSeconds(uptimeSeconds);
                    return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
                }
            }
            catch
            {
                return "N/A";
            }
        }

        // Data classes for structured metrics
        private class SystemMetrics
        {
            public DateTime Timestamp { get; set; }
            public double CpuLoad { get; set; }
            public MemoryInfo MemoryInfo { get; set; } = new();
            public DiskInfo DiskInfo { get; set; } = new();
            public SystemDetails SystemInfo { get; set; } = new();
        }

        private class MemoryInfo
        {
            public long TotalMB { get; set; }
            public long AvailableMB { get; set; }
            public long UsedMB { get; set; }
            public double UsagePercent { get; set; }
        }

        private class DiskInfo
        {
            public double TotalGB { get; set; }
            public double FreeGB { get; set; }
            public double UsedGB { get; set; }
            public double UsagePercent { get; set; }
            public string DriveName { get; set; } = string.Empty;
        }

        private class SystemDetails
        {
            public string OSPlatform { get; set; } = string.Empty;
            public string OSDescription { get; set; } = string.Empty;
            public string OSArchitecture { get; set; } = string.Empty;
            public string ProcessArchitecture { get; set; } = string.Empty;
            public string FrameworkDescription { get; set; } = string.Empty;
            public string MachineName { get; set; } = string.Empty;
            public int ProcessorCount { get; set; }
            public string Uptime { get; set; } = string.Empty;
        }

        // Windows memory status structure
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
