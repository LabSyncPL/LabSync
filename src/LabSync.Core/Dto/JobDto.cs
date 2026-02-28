using LabSync.Core.Types;

namespace LabSync.Core.Dto;

public record CreateJobRequest(
    string Command,
    string Arguments = "",
    string? ScriptPayload = null
);

public record JobDto
{
    public Guid Id { get; init; }
    public Guid DeviceId { get; init; }
    public string Command { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public JobStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public string? Output { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}

public record JobResultDto(
    Guid JobId,
    int ExitCode,
    string Output = ""
);