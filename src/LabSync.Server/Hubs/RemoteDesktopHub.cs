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
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RemoteDesktopHub(
    ConnectionTracker connectionTracker,
    IHubContext<AgentHub> agentHubContext,
    ILogger<RemoteDesktopHub> logger)
    : Hub
{
    private static string DeviceGroup(Guid deviceId) => $"device:{deviceId:N}";

    /// <summary>
    /// Viewer joins a group for a specific device to receive offers and ICE.
    /// </summary>
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

    /// <summary>
    /// Viewer requests to start a remote desktop session.
    /// This method signals the agent to begin streaming.
    /// </summary>
    public async Task RequestSession(Guid deviceId)
    {
        var agentConnectionId = connectionTracker.GetConnectionId(deviceId);
        if (agentConnectionId is null)
        {
            logger.LogWarning("RequestSession: no connected agent for device {DeviceId}.", deviceId);
            throw new HubException("Device is not connected.");
        }

        // Add the viewer to the device group so they can receive offers
        await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));
        
        var sessionId = Guid.NewGuid();
        logger.LogInformation("Requesting session {SessionId} from agent {AgentConnectionId} for viewer {ViewerConnectionId}.",
            sessionId, agentConnectionId, Context.ConnectionId);

        // Signal the agent to start the session
        await agentHubContext.Clients.Client(agentConnectionId)
            .SendAsync("StartRemoteDesktopSession", sessionId);
    }

    /// <summary>
    /// Viewer sends SDP answer back to the agent.
    /// </summary>
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

    /// <summary>
    /// Viewer sends ICE candidate back to the agent.
    /// </summary>
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

    // Server-to-viewer messages (called from AgentHub via group):
    // - ReceiveRemoteDesktopOffer(Guid sessionId, Guid deviceId, string sdpType, string sdp)
    // - ReceiveRemoteDesktopIceCandidate(Guid sessionId, string candidate, string? sdpMid, int? sdpMLineIndex)
}

