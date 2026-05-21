using System.Collections.Concurrent;

namespace LabSync.Server.Services;

public record AgentLogEntry(DateTime Timestamp, string Level, string Message);

public class AgentLogBuffer
{
    private const int MaxLogsPerDevice = 200;
    private readonly ConcurrentDictionary<Guid, Queue<AgentLogEntry>> _store = new();

    public void Push(Guid deviceId, AgentLogEntry entry)
    {
        var queue = _store.GetOrAdd(deviceId, _ => new Queue<AgentLogEntry>());
        lock (queue)
        {
            queue.Enqueue(entry);
            while (queue.Count > MaxLogsPerDevice)
                queue.Dequeue();
        }
    }

    public IReadOnlyList<AgentLogEntry> GetLogs(Guid deviceId)
    {
        if (!_store.TryGetValue(deviceId, out var queue))
            return [];
        lock (queue)
            return queue.ToList();
    }
}