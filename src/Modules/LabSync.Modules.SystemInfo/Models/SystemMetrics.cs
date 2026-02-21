using System;
using System.Collections.Generic;

namespace LabSync.Modules.SystemInfo.Models
{
    public sealed class SystemMetrics
    {
        public DateTime Timestamp { get; set; }
        public double CpuLoad { get; set; }
        public MemoryInfo MemoryInfo { get; set; } = new();
        public DiskInfo DiskInfo { get; set; } = new();
        public SystemDetails SystemInfo { get; set; } = new();
        public NetworkInfo NetworkInfo { get; set; } = new();
    }

    public sealed class MemoryInfo
    {
        public long TotalMB { get; set; }
        public long AvailableMB { get; set; }
        public long UsedMB { get; set; }
        public double UsagePercent { get; set; }
    }

    public sealed class DiskInfo
    {
        public double TotalGB { get; set; }
        public double FreeGB { get; set; }
        public double UsedGB { get; set; }
        public double UsagePercent { get; set; }
        public string DriveName { get; set; } = string.Empty;
        public List<DiskVolumeInfo> Volumes { get; set; } = new();
    }

    public sealed class DiskVolumeInfo
    {
        public string Name { get; set; } = string.Empty;
        public double TotalGB { get; set; }
        public double FreeGB { get; set; }
        public double UsedGB { get; set; }
        public double UsagePercent { get; set; }
    }

    public sealed class SystemDetails
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

    public sealed class NetworkInfo
    {
        public double TotalBytesSentPerSecond { get; set; }
        public double TotalBytesReceivedPerSecond { get; set; }
        public List<NetworkInterfaceInfo> Interfaces { get; set; } = new();
    }

    public sealed class NetworkInterfaceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long BytesSentPerSecond { get; set; }
        public long BytesReceivedPerSecond { get; set; }
        public string? IPv4Address { get; set; }
        public bool IsUp { get; set; }
    }
}

