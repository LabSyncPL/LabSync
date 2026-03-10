using System.Collections.Generic;

namespace LabSync.Modules.SystemInfo.Models
{
    public class HardwareInfo
    {
        public string CpuName { get; set; } = "Unknown";
        public string GpuName { get; set; } = "Unknown"; 
        public string TotalRam { get; set; } = "Unknown";
        public List<DiskSpec> Disks { get; set; } = new();
        public List<NetworkAdapterSpec> NetworkAdapters { get; set; } = new();
    }

    public class DiskSpec
    {
        public string Model { get; set; } = "Unknown";
        public string Size { get; set; } = "Unknown";
        public string Type { get; set; } = "Unknown";
    }

    public class NetworkAdapterSpec
    {
        public string Name { get; set; } = "Unknown";
        public string MacAddress { get; set; } = "Unknown";
        public string Status { get; set; } = "Unknown";
    }
}
