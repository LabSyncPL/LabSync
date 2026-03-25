namespace LabSync.Modules.ScriptExecutor.Models;

public enum InterpreterType
{
    PowerShell = 0,
    Bash = 1,
    Cmd = 2,
}

public sealed record CommandEnvelope(
    string ScriptContent,
    InterpreterType InterpreterType,
    string[]? Arguments = null,
    int TimeoutSeconds = 300,
    Guid? TaskId = null,
    Guid? MachineId = null);

public sealed record ExecutionResult(
    int ExitCode,
    TimeSpan TotalRunTime,
    bool IsSuccess
);

