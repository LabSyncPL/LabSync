namespace LabSync.Core.Entities;

public class AgentLog
{
    //TimescaleDB requires a timestamp column
    public DateTime Timestamp { get; init; }

    public Guid DeviceId { get; init; }
    public Device? Device { get; private set; }

    public double? CpuUsagePercentage { get; init; }
    public double? RamUsageMegabytes { get; init; }
    public string? StatusMessage { get; init; }

    protected AgentLog() { }

    //for real-time logging
    public AgentLog(Guid deviceId, double? cpu, double? ram, string? message = null)
    {
        Timestamp = DateTime.UtcNow;
        DeviceId = deviceId;
        CpuUsagePercentage = cpu;
        RamUsageMegabytes = ram;
        StatusMessage = message;
    }

    //for historical log entries
    public AgentLog(Guid deviceId, DateTime timestamp, double? cpu, double? ram, string? message = null)
    {
        Timestamp = timestamp;
        DeviceId = deviceId;
        CpuUsagePercentage = cpu;
        RamUsageMegabytes = ram;
        StatusMessage = message;
    }
}