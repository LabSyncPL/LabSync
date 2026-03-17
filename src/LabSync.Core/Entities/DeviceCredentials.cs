using System;
using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Entities;

public class DeviceCredentials
{
    public Guid Id { get; init; }
    public Guid DeviceId { get; init; }
    
    [Required]
    [MaxLength(100)]
    public string SshUsername { get; set; } = string.Empty;

    // TODO: This should be encrypted.
    [MaxLength(500)]
    public string SshPassword { get; set; } = string.Empty;
    public string? SshPrivateKey { get; set; }

    public Device? Device { get; set; }

    protected DeviceCredentials() { }

    public DeviceCredentials(Guid deviceId, string username, string password)
    {
        Id = Guid.NewGuid();
        DeviceId = deviceId;
        SshUsername = username;
        SshPassword = password;
    }
}
