using System;
using System.Collections.Generic;
using LabSync.Core.Dto;

namespace LabSync.Core.Entities;

public enum ScheduledScriptTargetType
{
    SingleAgent = 0,
    Group = 1
}

public class ScheduledScript
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string ScriptContent { get; private set; } = "";
    public ScriptInterpreterType InterpreterType { get; private set; }
    public string[] Arguments { get; private set; } = [];
    public int TimeoutSeconds { get; private set; } = 300;

    public string? CronExpression { get; private set; }
    public DateTimeOffset? RunAt { get; private set; }

    public bool IsEnabled { get; private set; } = true;
    public DateTimeOffset? LastRunAt { get; private set; }
    public DateTimeOffset? NextRunAt { get; private set; }

    public ScheduledScriptTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }

    public ICollection<ScheduledScriptExecution> Executions { get; private set; } = new List<ScheduledScriptExecution>();

    private ScheduledScript() { } // EF Core

    public ScheduledScript(
        string name,
        string scriptContent,
        ScriptInterpreterType interpreterType,
        string[]? arguments,
        int timeoutSeconds,
        string? cronExpression,
        DateTimeOffset? runAt,
        ScheduledScriptTargetType targetType,
        Guid targetId,
        string? createdBy = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        ScriptContent = scriptContent;
        InterpreterType = interpreterType;
        Arguments = arguments ?? [];
        TimeoutSeconds = timeoutSeconds;
        CronExpression = cronExpression;
        RunAt = runAt;
        TargetType = targetType;
        TargetId = targetId;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
    }

    public void Update(
        string name,
        string scriptContent,
        ScriptInterpreterType interpreterType,
        string[]? arguments,
        int timeoutSeconds,
        string? cronExpression,
        DateTimeOffset? runAt,
        ScheduledScriptTargetType targetType,
        Guid targetId)
    {
        Name = name;
        ScriptContent = scriptContent;
        InterpreterType = interpreterType;
        Arguments = arguments ?? [];
        TimeoutSeconds = timeoutSeconds;
        CronExpression = cronExpression;
        RunAt = runAt;
        TargetType = targetType;
        TargetId = targetId;
    }

    public void SetNextRunAt(DateTimeOffset? nextRunAt)
    {
        NextRunAt = nextRunAt;
    }

    public void MarkRun(DateTimeOffset runAt)
    {
        LastRunAt = runAt;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
