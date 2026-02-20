using LabSync.Core.ValueObjects;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace LabSync.Core.Entities
{
    public class Device
    {
        /// <summary>
        /// Unique identifier for the device in the database.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Network hostname (e.g., "DESKTOP-5A2K", "ubuntu-lab-01").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Hostname { get; set; } = string.Empty;

        /// <summary>
        /// Is the device trusted?
        /// </summary>
        public bool IsApproved { get; set; } = false;

        /// <summary>
        /// Physical hardware address. Must match the format XX:XX:XX:XX:XX:XX.
        /// </summary>
        [Required]
        [RegularExpression(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$", ErrorMessage = "Invalid MAC Address format.")]
        public string MacAddress { get; set; } = string.Empty;

        /// <summary>
        /// Last known IP address (IPv4 or IPv6). 
        /// Used for diagnostics and direct connections (e.g., VNC).
        /// </summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// Operating System family (Windows/Linux).
        /// </summary>
        public DevicePlatform Platform { get; set; }

        /// <summary>
        /// Detailed OS version string (e.g., "Windows 11 Pro 22H2", "Ubuntu 22.04 LTS").
        /// </summary>
        [MaxLength(200)]
        public string OsVersion { get; set; } = string.Empty;

        /// <summary>
        /// Lifecycle status of the device (Pending/Active/Maintenance/Blocked).
        /// </summary>
        public DeviceStatus Status { get; set; } = DeviceStatus.Pending;

        /// <summary>
        /// Technical state managed by SignalR (True if connected).
        /// </summary>
        public bool IsOnline { get; set; } = false;

        /// <summary>
        /// Timestamp when the agent first registered.
        /// </summary>
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of the last heartbeat received from the agent.
        /// Used to calculate runtime "Online/Offline" availability.
        /// </summary>
        public DateTime? LastSeenAt { get; set; }

        /// <summary>
        /// Optional ID of the group (e.g., specific lab or department) this device belongs to.
        /// </summary>
        public Guid? GroupId { get; set; }

        /// <summary>
        /// Navigation property for the assigned DeviceGroup.
        /// </summary>
        public DeviceGroup? Group { get; set; }

        /// <summary>
        /// Flexible metadata for hardware specifications (e.g., CPU, total RAM, Disk size).
        /// Mapped to a JSONB column in PostgreSQL.
        /// </summary>
        public JsonDocument? HardwareInfo { get; set; }

        /// <summary>
        /// Hash of the secret key used for agent authentication.
        /// </summary>
        [Required]
        [MaxLength(256)]
        public string DeviceKeyHash { get; set; } = string.Empty;

        /// <summary>
        /// Jobs that were executed or scheduled for this device.
        /// </summary>
        public ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}