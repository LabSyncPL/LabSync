using LabSync.Core.ValueObjects;
using System.Text.Json;

namespace LabSync.Core.Dto
{
    /// <summary>
    /// Data Transfer Object for returning device information to clients.
    /// This prevents exposing the full database entity.
    /// </summary>
    public class DeviceDto
    {
        public Guid Id { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DevicePlatform Platform { get; set; }
        public string OsVersion { get; set; } = string.Empty;
        public DeviceStatus Status { get; set; }
        public bool IsOnline { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public Guid? GroupId { get; set; }
        public string? GroupName { get; set; }
        public string? HardwareInfo { get; set; }
    }
}
