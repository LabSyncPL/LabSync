using LabSync.Core.Types;
using System;

namespace LabSync.Core.Dto;

public record DeviceDto
{
    public Guid Id { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public bool IsApproved { get; init; }
    public string MacAddress { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public DevicePlatform Platform { get; init; }
    public string OsVersion { get; init; } = string.Empty;
    public DeviceStatus Status { get; init; }
    public bool IsOnline { get; init; }
    public DateTime RegisteredAt { get; init; }
    public DateTime? LastSeenAt { get; init; }

    public Guid? GroupId { get; init; }
    public string? GroupName { get; init; }

}
