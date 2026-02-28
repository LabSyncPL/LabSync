using LabSync.Core.Types;

namespace LabSync.Core.Entities;

public class Job
{
    public Guid Id { get; init; }

    public Guid DeviceId { get; init; }
    public Device? Device { get; private set; }

    public string Command { get; init; }
    public string Arguments { get; init; }
    public string? ScriptPayload { get; init; }

    public JobStatus Status { get; private set; }
    public int? ExitCode { get; private set; }
    public string? Output { get; private set; }

    public DateTime CreatedAt { get; init; }
    public DateTime? FinishedAt { get; private set; }

    protected Job() { }

    public Job(Guid deviceId, string command, string arguments = "", string? scriptPayload = null)
    {
        Id = Guid.NewGuid();
        DeviceId      = deviceId;
        Command       = command;
        Arguments     = arguments;
        ScriptPayload = scriptPayload;
        Status        = JobStatus.Pending;
        CreatedAt     = DateTime.UtcNow;
    }

    public void MarkAsRunning()
    {
        if (Status != JobStatus.Pending)
            throw new InvalidOperationException("Only pending tasks can be started.");

        Status = JobStatus.Running;
    }

    public void Complete(int exitCode, string? output)
    {
        if (Status != JobStatus.Running)
            throw new InvalidOperationException("You can only complete tasks that are currently running.");

        ExitCode   = exitCode;
        Output     = output;
        Status     = exitCode == 0 ? JobStatus.Completed : JobStatus.Failed;
        FinishedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == JobStatus.Completed || Status == JobStatus.Failed)
            return;

        Status     = JobStatus.Cancelled;
        FinishedAt = DateTime.UtcNow;
    }
}