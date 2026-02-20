using System.Collections.Concurrent;

namespace LabSync.Server.Services;

/// <summary>
/// Tracks active SignalR connections for each device. One connection per device;
/// when a device reconnects, the previous connection is replaced.
/// </summary>
public class ConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, string> _deviceToConnection = new();

    public void Add(Guid deviceId, string connectionId)
    {
        _deviceToConnection[deviceId] = connectionId;
    }

    public void Remove(Guid deviceId)
    {
        _deviceToConnection.TryRemove(deviceId, out _);
    }

    public string? GetConnectionId(Guid deviceId)
    {
        return _deviceToConnection.TryGetValue(deviceId, out var connectionId) ? connectionId : null;
    }

    public bool IsConnected(Guid deviceId)
    {
        return _deviceToConnection.ContainsKey(deviceId);
    }

    public IReadOnlyDictionary<Guid, string> GetConnections()
    {
        return _deviceToConnection;
    }
}
