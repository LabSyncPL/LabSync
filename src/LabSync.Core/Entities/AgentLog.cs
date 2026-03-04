namespace LabSync.Core.Entities;

public class AgentLog
{
    //TimescaleDB opiera się na czasie, więc to pole jest kluczowe
    public DateTime Timestamp { get; init; }

    // Klucz obcy encji Device
    public Guid DeviceId { get; init; }
    public Device? Device { get; private set; }

    public double? CpuUsagePercentage { get; init; }
    public double? RamUsageMegabytes { get; init; }
    public string? StatusMessage { get; init; }

    protected AgentLog() { }

    public AgentLog(Guid deviceId, double? cpu, double? ram, string? message = null)
    {
        Timestamp = DateTime.UtcNow;
        DeviceId = deviceId;
        CpuUsagePercentage = cpu;
        RamUsageMegabytes = ram;
        StatusMessage = message;
    }
}