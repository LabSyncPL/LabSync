using System;
using LabSync.Core.Entities;

namespace LabSync.Core.Dto;

public class CreateScheduledScriptDto
{
    public string Name { get; set; } = "";
    public string ScriptContent { get; set; } = "";
    public ScriptInterpreterType InterpreterType { get; set; }
    public string[] Arguments { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 300;
    public string? CronExpression { get; set; }
    public DateTimeOffset? RunAt { get; set; }
    public ScheduledScriptTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
}

public class UpdateScheduledScriptDto
{
    public string Name { get; set; } = "";
    public string ScriptContent { get; set; } = "";
    public ScriptInterpreterType InterpreterType { get; set; }
    public string[] Arguments { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 300;
    public string? CronExpression { get; set; }
    public DateTimeOffset? RunAt { get; set; }
    public ScheduledScriptTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public bool IsEnabled { get; set; }
}

public class ScheduledScriptDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ScriptContent { get; set; } = "";
    public ScriptInterpreterType InterpreterType { get; set; }
    public string[] Arguments { get; set; } = [];
    public int TimeoutSeconds { get; set; }
    public string? CronExpression { get; set; }
    public DateTimeOffset? RunAt { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public ScheduledScriptTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ScheduledScriptExecutionDto
{
    public Guid Id { get; set; }
    public Guid ScheduledScriptId { get; set; }
    public Guid TaskId { get; set; }
    public DateTimeOffset ScheduledTime { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
}
