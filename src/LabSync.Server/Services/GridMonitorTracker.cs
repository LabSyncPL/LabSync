using System.Collections.Concurrent;

namespace LabSync.Server.Services;

public class GridMonitorTracker
{
    // Tracks the number of active subscriptions per device
    private readonly ConcurrentDictionary<Guid, int> _deviceViewerCounts = new();
    
    // Tracks which devices a specific viewer connection is subscribed to
    private readonly ConcurrentDictionary<string, HashSet<Guid>> _connectionSubscriptions = new();

    /// <summary>
    /// Adds a subscription for a connection to a device monitor.
    /// Returns true if this is the first viewer for the device (should start agent).
    /// </summary>
    public bool AddSubscription(string connectionId, Guid deviceId)
    {
        var subscriptions = _connectionSubscriptions.GetOrAdd(connectionId, _ => new HashSet<Guid>());
        lock (subscriptions)
        {
            if (!subscriptions.Add(deviceId))
            {
                return false; // Already subscribed
            }
        }

        var newCount = _deviceViewerCounts.AddOrUpdate(deviceId, 1, (_, count) => count + 1);
        return newCount == 1; // True if we transitioned from 0 to 1
    }

    /// <summary>
    /// Removes a subscription for a connection from a device monitor.
    /// Returns true if this was the last viewer for the device (should stop agent).
    /// </summary>
    public bool RemoveSubscription(string connectionId, Guid deviceId)
    {
        if (_connectionSubscriptions.TryGetValue(connectionId, out var subscriptions))
        {
            lock (subscriptions)
            {
                if (!subscriptions.Remove(deviceId))
                {
                    return false; // Was not subscribed
                }
            }
        }
        else
        {
            return false;
        }

        while (true)
        {
            if (!_deviceViewerCounts.TryGetValue(deviceId, out var count))
            {
                return false;
            }

            if (count <= 1)
            {
                if (_deviceViewerCounts.TryRemove(deviceId, out _))
                {
                    return true; // Transitioned to 0
                }
            }
            else
            {
                if (_deviceViewerCounts.TryUpdate(deviceId, count - 1, count))
                {
                    return false; // Still has viewers
                }
            }
        }
    }

    /// <summary>
    /// Removes all subscriptions for a connection.
    /// Returns a list of device IDs that no longer have any viewers (should stop agent).
    /// </summary>
    public List<Guid> RemoveAllSubscriptions(string connectionId)
    {
        var devicesToStop = new List<Guid>();

        if (_connectionSubscriptions.TryRemove(connectionId, out var subscriptions))
        {
            HashSet<Guid> subsCopy;
            lock (subscriptions)
            {
                subsCopy = new HashSet<Guid>(subscriptions);
            }

            foreach (var deviceId in subsCopy)
            {
                while (true)
                {
                    if (!_deviceViewerCounts.TryGetValue(deviceId, out var count))
                    {
                        break;
                    }

                    if (count <= 1)
                    {
                        if (_deviceViewerCounts.TryRemove(deviceId, out _))
                        {
                            devicesToStop.Add(deviceId);
                            break;
                        }
                    }
                    else
                    {
                        if (_deviceViewerCounts.TryUpdate(deviceId, count - 1, count))
                        {
                            break;
                        }
                    }
                }
            }
        }

        return devicesToStop;
    }

    /// <summary>
    /// Checks if a device currently has any active grid monitor viewers.
    /// </summary>
    public bool HasViewers(Guid deviceId)
    {
        return _deviceViewerCounts.TryGetValue(deviceId, out var count) && count > 0;
    }
}