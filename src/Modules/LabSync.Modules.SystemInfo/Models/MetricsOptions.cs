namespace LabSync.Modules.SystemInfo.Models
{
    public sealed class MetricsOptions
    {
        public bool CollectCpu { get; set; }
        public bool CollectMemory { get; set; }
        public bool CollectDisks { get; set; }
        public bool CollectNetwork { get; set; }
        public bool CollectProcesses { get; set; }
        public bool CollectSystemInfo { get; set; }
        public bool CollectAll { get; set; }

        public static MetricsOptions All => new() 
        { 
            CollectAll = true,
            CollectCpu = true, 
            CollectMemory = true, 
            CollectDisks = true, 
            CollectNetwork = true, 
            CollectProcesses = true,
            CollectSystemInfo = true
        };
    }
}