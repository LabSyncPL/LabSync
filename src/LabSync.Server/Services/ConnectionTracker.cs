using System.Collections.Concurrent;

namespace LabSync.Server.Services;

/// <summary>
/// A singleton service to track active SignalR connections for each device.
/// Maps DeviceId to SignalR ConnectionId.
/// </summary>
public class ConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, string> _connections = new();

    /// <summary>
    /// Adds a device connection to the tracker.
    /// </summary>
    /// <param name="deviceId">The device's unique identifier.</param>
    /// <param name="connectionId">The SignalR connection ID.</param>
    public void Add(Guid deviceId, string connectionId)
    {
        _connections[deviceId] = connectionId;
    }

    /// <summary>
    /// Removes a device connection from the tracker.
    /// </summary>
    /// <param name="deviceId">The device's unique identifier.</param>
    public void Remove(Guid deviceId)
    {
        _connections.TryRemove(deviceId, out _);
    }

    /// <summary>
    /// Gets the connection ID for a given device.
    /// </summary>
    /// <param name="deviceId">The device's unique identifier.</param>
    /// <returns>The connection ID if found, otherwise null.</returns>
    public string? GetConnectionId(Guid deviceId)
    {
        return _connections.TryGetValue(deviceId, out var connectionId) ? connectionId : null;
    }

    /// <summary>
    /// Gets all tracked connections.
    /// </summary>
    /// <returns>A dictionary of all tracked connections.</returns>
    public IReadOnlyDictionary<Guid, string> GetConnections()
    {
        return _connections;
    }
}
