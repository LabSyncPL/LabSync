using LabSync.Core.ValueObjects;
using LabSync.Server.Data;
using LabSync.Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LabSync.Server.Hubs;

/// <summary>
/// Strongly-typed SignalR Hub for real-time communication with authenticated LabSync Agents.
/// This hub serves as the core of the 'Control Plane' for agent management.
/// </summary>
[Authorize]
public class AgentHub : Hub<IAgentClient>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(IServiceScopeFactory scopeFactory, ILogger<AgentHub> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Called when a new agent connects. It updates the device's status to 'Online'.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == null)
        {
            _logger.LogWarning("Connection attempt rejected. Could not resolve device identifier from token.");
            Context.Abort();
            return;
        }

        _logger.LogInformation("Agent connected: {DeviceId}", deviceId);
        await UpdateDeviceStatus(deviceId, DeviceStatus.Online);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when an agent disconnects. It updates the device's status to 'Offline'.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Agent disconnected due to an error. DeviceId: {DeviceId}", GetDeviceIdFromContext());
        }
        else
        {
            _logger.LogInformation("Agent disconnected: {DeviceId}", GetDeviceIdFromContext());
        }

        var deviceId = GetDeviceIdFromContext();
        if (deviceId != null)
        {
            await UpdateDeviceStatus(deviceId, DeviceStatus.Offline);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Retrieves the device identifier from the authenticated user's claims.
    /// The 'NameIdentifier' claim is populated with the Device's MAC address during token generation.
    /// </summary>
    /// <returns>The device identifier (MAC address) or null if not found.</returns>
    private string? GetDeviceIdFromContext()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Updates the device status and last seen timestamp in the database.
    /// This method creates a new DI scope to safely resolve the scoped DbContext
    /// within the singleton lifecycle of the Hub.
    /// </summary>
    /// <param name="deviceId">The unique identifier of the device.</param>
    /// <param name="status">The new status to set.</param>
    private async Task UpdateDeviceStatus(string deviceId, DeviceStatus status)
{
    using var scope = _scopeFactory.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LabSyncDbContext>();

    if (Guid.TryParse(deviceId, out var parsedId))
    {
        var device = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == parsedId);

        if (device != null)
        {
            device.Status = status;
            device.LastSeenAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully updated status for device {DeviceId} to {Status}", deviceId, status);
        }
        else
        {
            _logger.LogWarning("Could not find device with ID {DeviceId} to update its status.", deviceId);
        }
    }
    else
    {
         _logger.LogError("Error parsing DeviceId. Expected GUID, received: {DeviceId}", deviceId);
    }
}
}
