namespace LabSync.Core.Dto;

/// <summary>
/// Mirrors LabSync.Modules.ScriptExecutor.Models.InterpreterType for API contracts.
/// </summary>
public enum ScriptInterpreterType
{
    PowerShell = 0,
    Bash = 1,
    Cmd = 2,
}

/// <summary>
/// Request body for POST /api/script-runner/execute.
/// Aligns with CommandEnvelope (script content, interpreter, timeout, args) plus multi-target IDs.
/// </summary>
public sealed class ExecuteScriptRequest
{
    public string ScriptContent { get; set; } = "";
    public ScriptInterpreterType InterpreterType { get; set; }
    public Guid[] TargetMachineIds { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 300;
    public string[]? Arguments { get; set; }
}

public sealed class ExecuteScriptResponse
{
    public Guid JobId { get; set; }
    public string[]? DispatchWarnings { get; set; }
}

public sealed class CancelScriptTaskRequest
{
    public Guid TaskId { get; set; }
    public Guid? MachineId { get; set; }
}

/// <summary>
/// Payload streamed to the UI via ScriptHub (and sent from the agent via AgentHub).
/// Extends script line telemetry with correlation IDs.
/// </summary>
public sealed record ScriptOutputTelemetryDto(
    Guid? TaskId,
    Guid? MachineId,
    string Interpreter,
    string Stream,
    string Line,
    DateTimeOffset TimestampUtc);
