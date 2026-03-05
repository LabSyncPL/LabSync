using LabSync.Core.Dto;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LabSync.Server.Hubs;

/// <summary>
/// Hub for viewers (web UI) to receive remote desktop offers from agents
/// and send back answers and ICE candidates.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," + LabSync.Server.Authentication.DeviceKeyAuthenticationHandler.SchemeName)]
public class RemoteDesktopHub(
    ConnectionTracker connectionTracker,
    IHubContext<AgentHub> agentHubContext,
    ILogger<RemoteDesktopHub> logger)
    : Hub
{
    private static string DeviceGroup(Guid deviceId) => $"device:{deviceId:N}";

    public async Task JoinDevice(Guid deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));
        logger.LogInformation("Connection {ConnectionId} joined remote desktop group for device {DeviceId}.",
            Context.ConnectionId, deviceId);
    }

    public async Task LeaveDevice(Guid deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));
        logger.LogInformation("Connection {ConnectionId} left remote desktop group for device {DeviceId}.",
            Context.ConnectionId, deviceId);
    }

    public async Task RequestSession(Guid deviceId, RemoteDesktopPreferencesDto? preferences = null)
    {
        var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
        if (agentConnectionId is null)
        {
            logger.LogWarning("RequestSession: no connected agent for device {DeviceId}.", deviceId);
            throw new HubException("Device is not connected.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));
        
        var sessionId = Guid.NewGuid();
        logger.LogInformation("Requesting session {SessionId} from agent {AgentConnectionId} for viewer {ViewerConnectionId}. Prefs: {@Prefs}",
            sessionId, agentConnectionId, Context.ConnectionId, preferences);

        await agentHubContext.Clients.Client(agentConnectionId)
            .SendAsync("StartRemoteDesktopSession", sessionId, preferences);
    }

    public async Task StopSession(Guid deviceId, Guid sessionId)
    {
        var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
        if (agentConnectionId is null)
        {
            logger.LogWarning("StopSession: no connected agent for device {DeviceId}.", deviceId);
            return;
        }

        logger.LogInformation("Stopping session {SessionId} on agent {AgentConnectionId}.", sessionId, agentConnectionId);

        await agentHubContext.Clients.Client(agentConnectionId)
            .SendAsync("StopRemoteDesktopSession", sessionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));
    }

    public async Task SendRemoteDesktopAnswer(Guid sessionId, Guid deviceId, string sdpType, string sdp)
    {
        var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
        if (agentConnectionId is null)
        {
            logger.LogWarning("SendRemoteDesktopAnswer: no connected agent for device {DeviceId}.", deviceId);
            return;
        }

        logger.LogInformation("Forwarding RemoteDesktopAnswer for session {SessionId} to agent connection {ConnectionId}.",
            sessionId, agentConnectionId);

        await agentHubContext.Clients.Client(agentConnectionId)
            .SendAsync("RemoteDesktopAnswer", sessionId, sdpType, sdp);
    }

    public async Task SendIceCandidate(Guid sessionId, Guid deviceId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
        if (agentConnectionId is null)
        {
            logger.LogWarning("SendIceCandidate: no connected agent for device {DeviceId}.", deviceId);
            return;
        }

        await agentHubContext.Clients.Client(agentConnectionId)
            .SendAsync("RemoteDesktopIceCandidate", sessionId, candidate, sdpMid, sdpMLineIndex);
    }

    private static string MonitorGroup(Guid deviceId) => $"Monitor_{deviceId:N}";

    public async Task SubscribeToMonitor(List<Guid> deviceIds)
    {
        foreach (var deviceId in deviceIds)
        {
            var groupName = MonitorGroup(deviceId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
            if (agentConnectionId != null)
            {
                await agentHubContext.Clients.Client(agentConnectionId).SendAsync("StartMonitor");
            }
        }
    }

    public async Task ConfigureMonitor(List<Guid> deviceIds, int width, int quality, int fps)
    {
        foreach (var deviceId in deviceIds)
        {
            var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
            if (agentConnectionId != null)
            {
                await agentHubContext.Clients.Client(agentConnectionId)
                    .SendAsync("ConfigureMonitor", width, quality, fps);
            }
        }
    }

    public async Task UnsubscribeFromMonitor(List<Guid> deviceIds)
    {
        foreach (var deviceId in deviceIds)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, MonitorGroup(deviceId));
        }
    }

    public async Task SendRemoteDesktopIceCandidate(Guid sessionId, Guid deviceId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
        if (agentConnectionId is null)
        {
            logger.LogWarning("SendRemoteDesktopIceCandidate: no connected agent for device {DeviceId}.", deviceId);
            return;
        }

        logger.LogTrace("Forwarding ICE candidate for session {SessionId} to agent connection {ConnectionId}.",
            sessionId, agentConnectionId);

        await agentHubContext.Clients.Client(agentConnectionId)
            .SendAsync("RemoteDesktopIceCandidate", sessionId, candidate, sdpMid, sdpMLineIndex);
    }

}

