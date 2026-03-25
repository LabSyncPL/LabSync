using System.Collections.Concurrent;

namespace LabSync.Server.Services;

/// <summary>
/// In-memory correlation between a logical script task (JobId from execute response) and per-device jobs.
/// </summary>
public sealed class ScriptTaskRegistry
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Guid>> _taskToDeviceJobs = new();

    public void Register(Guid taskId, Guid deviceId, Guid jobId)
    {
        var inner = _taskToDeviceJobs.GetOrAdd(taskId, _ => new ConcurrentDictionary<Guid, Guid>());
        inner[deviceId] = jobId;
    }

    public IReadOnlyCollection<(Guid DeviceId, Guid JobId)> GetJobs(Guid taskId)
    {
        if (!_taskToDeviceJobs.TryGetValue(taskId, out var inner))
            return Array.Empty<(Guid, Guid)>();

        return inner.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public void RemoveTask(Guid taskId)
    {
        _taskToDeviceJobs.TryRemove(taskId, out _);
    }
}
