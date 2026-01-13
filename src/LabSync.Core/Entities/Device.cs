using LabSync.Core.ValueObjects;
using System.ComponentModel.DataAnnotations;
using System;

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
        /// Physical MAC address. Used as a unique fingerprint for hardware identification.
        /// Format: XX:XX:XX:XX:XX:XX
        /// </summary>
        [Required]
        [MaxLength(17)]
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
        /// Lifecycle status of the device (Pending/Active/Blocked).
        /// Determines if the device is allowed to receive tasks.
        /// </summary>
        public DeviceStatus Status { get; set; } = DeviceStatus.Pending;

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
        /// Security token used by the Agent for API authentication.
        /// TODO: In production, consider hashing this value.
        /// </summary>
        [MaxLength(256)]
        public string AgentToken { get; set; } = string.Empty;
    }
}