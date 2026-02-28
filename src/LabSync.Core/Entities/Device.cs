using LabSync.Core.Types;
using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Entities;

public class Device
{
    public Guid Id { get; init; }
    public string Hostname { get; private set; }
    public bool IsApproved { get; private set; }
    public string MacAddress { get; init; }
    public string? IpAddress { get; private set; }
    public DevicePlatform Platform { get; private set; }
    public string OsVersion { get; private set; }
    public DeviceStatus Status { get; private set; }
    public bool IsOnline { get; private set; }
    public DateTime RegisteredAt { get; init; }
    public DateTime? LastSeenAt { get; private set; }

    public Guid? GroupId { get; private set; }
    public DeviceGroup? Group { get; private set; }

    public string DeviceKeyHash { get; private set; }

    private readonly List<Job> _jobs = new();
    public IReadOnlyCollection<Job> Jobs => _jobs.AsReadOnly();

    protected Device() { }

    public Device(string hostname, string macAddress, DevicePlatform platform, string osVersion, string deviceKeyHash)
    {
        Id = Guid.NewGuid();
        Hostname      = hostname;
        MacAddress    = macAddress;
        Platform      = platform;
        OsVersion     = osVersion;
        DeviceKeyHash = deviceKeyHash;
        RegisteredAt  = DateTime.UtcNow;
        Status        = DeviceStatus.Pending;
        IsApproved    = false;
        IsOnline      = false;
    }

    public void Approve()
    {
        IsApproved = true;
        Status     = DeviceStatus.Active;
    }

    public void MarkAsOffline()
    {
        IsOnline = false;
    }

    public void RecordHeartbeat(string ipAddress)
    {
        IsOnline   = true;
        IpAddress  = ipAddress;
        LastSeenAt = DateTime.UtcNow;
    }
}

