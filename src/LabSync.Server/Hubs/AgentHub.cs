using LabSync.Server.Authentication;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LabSync.Server.Hubs;

[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{DeviceKeyAuthenticationHandler.SchemeName}", Roles = "Agent")]
public class AgentHub(
    LabSyncDbContext dbContext,
    ConnectionTracker connectionTracker,
    ILogger<AgentHub> logger)
    : Hub<IAgentClient>
{
    public override async Task OnConnectedAsync()
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty)
        {
            // This should not happen if authentication is working correctly
            Context.Abort();
            return;
        }

        var connectionId = Context.ConnectionId;
        connectionTracker.Add(deviceId, connectionId);

        try
        {
            var device = await dbContext.Devices.FindAsync(deviceId);
            if (device != null)
            {
                device.IsOnline = true;
                device.LastSeenAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Device {DeviceId} connected with ConnectionId {ConnectionId}", deviceId, connectionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating device status on connect for DeviceId {DeviceId}", deviceId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId != Guid.Empty)
        {
            connectionTracker.Remove(deviceId);

            try
            {
                var device = await dbContext.Devices.FindAsync(deviceId);
                if (device != null)
                {
                    device.IsOnline = false;
                    // LastSeenAt is not updated on disconnect to preserve the last known online time
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Device {DeviceId} disconnected", deviceId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating device status on disconnect for DeviceId {DeviceId}", deviceId);
            }
        }

        if (exception != null)
        {
            logger.LogWarning(exception, "Device {DeviceId} disconnected with error", deviceId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetDeviceIdFromContext()
    {
        var deviceIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(deviceIdClaim, out var deviceId) ? deviceId : Guid.Empty;
    }
}
