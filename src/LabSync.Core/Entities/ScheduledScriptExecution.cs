using System;
using LabSync.Core.Types;

namespace LabSync.Core.Entities;

public class ScheduledScriptExecution
{
    public Guid Id { get; private set; }
    public Guid ScheduledScriptId { get; private set; }
    public ScheduledScript? ScheduledScript { get; private set; }

    public Guid TaskId { get; private set; }
    public DateTimeOffset ScheduledTime { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public string? Error { get; private set; }

    private ScheduledScriptExecution() { } 

    public ScheduledScriptExecution(Guid scheduledScriptId, Guid taskId, DateTimeOffset scheduledTime)
    {
        Id = Guid.NewGuid();
        ScheduledScriptId = scheduledScriptId;
        TaskId = taskId;
        ScheduledTime = scheduledTime;
        Status = JobStatus.Pending;
    }

    public void MarkStarted()
    {
        Status = JobStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = JobStatus.Completed;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string? error)
    {
        Status = JobStatus.Failed;
        FinishedAt = DateTimeOffset.UtcNow;
        Error = error;
    }
}
