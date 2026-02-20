using LabSync.Core.ValueObjects;

namespace LabSync.Core.Dto;

/// <summary>
/// Job data returned to API clients.
/// </summary>
public class JobDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
