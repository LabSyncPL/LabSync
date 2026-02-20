using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Dto;

/// <summary>
/// Request to create and dispatch a job to a device.
/// </summary>
public class CreateJobRequest
{
    [Required(ErrorMessage = "Command is required.")]
    [MaxLength(200)]
    public string Command { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Optional script payload (e.g. PowerShell/Bash). Use only for trusted, predefined script types.
    /// </summary>
    public string? ScriptPayload { get; set; }
}
