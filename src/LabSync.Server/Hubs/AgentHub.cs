using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using LabSync.Core.Types;
using LabSync.Server.Authentication;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LabSync.Server.Hubs;

[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{DeviceKeyAuthenticationHandler.SchemeName}", Roles = "Agent")]
public class AgentHub(
    LabSyncDbContext dbContext,
    ConnectionTracker _connectionTracker,
    IHubContext<RemoteDesktopHub> remoteDesktopHubContext,
    ILogger<AgentHub> logger)
    : Hub<IAgentClient>
{
    public override async Task OnConnectedAsync()
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty)
        {
            Context.Abort();
            logger.LogWarning("Connection attempt without valid DeviceId. ConnectionId: {ConnectionId}", Context.ConnectionId);
            return;
        }

        _connectionTracker.Add(deviceId, Context.ConnectionId);
        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device != null)
        {
            var ipAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            device.RecordHeartbeat(ipAddress);
        }

        var pendingJobs = await dbContext.Jobs
            .Where(j => j.DeviceId == deviceId && j.Status == JobStatus.Pending)
            .ToListAsync();

        foreach (var job in pendingJobs)
        {
            await Clients.Caller.ReceiveJob(job.Id, job.Command, job.Arguments, job.ScriptPayload);
            job.MarkAsRunning();
        }

        await dbContext.SaveChangesAsync();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId != Guid.Empty)
        {
            _connectionTracker.Remove(deviceId);
        }

        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.MarkAsOffline();
            await dbContext.SaveChangesAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string DeviceGroup(Guid deviceId) => $"device:{deviceId:N}";

    /// <summary>
    /// Called by agent module when it has created an SDP offer for remote desktop.
    /// Forwards the offer to all viewers subscribed to the device group.
    /// </summary>
    public async Task RemoteDesktopOffer(Guid sessionId, Guid deviceId, string sdpType, string sdp, string[]? availableEncoders = null)
    {
        var deviceIdFromContext = GetDeviceIdFromContext();
        if (deviceIdFromContext == Guid.Empty || deviceIdFromContext != deviceId)
        {
            logger.LogWarning("RemoteDesktopOffer: deviceId mismatch. FromContext={ContextDeviceId}, PayloadDeviceId={DeviceId}",
                deviceIdFromContext, deviceId);
            return;
        }

        logger.LogInformation("Received RemoteDesktopOffer for session {SessionId} from device {DeviceId}. Forwarding to viewers.",
            sessionId, deviceId);

        await remoteDesktopHubContext.Clients.Group(DeviceGroup(deviceId))
            .SendAsync("ReceiveRemoteDesktopOffer", sessionId, deviceId, sdpType, sdp, availableEncoders);
    }

    /// <summary>
    /// Called by agent module when it has gathered ICE candidates.
    /// Forwards candidates to all viewers in the device group.
    /// </summary>
    public async Task RemoteDesktopIceCandidate(Guid sessionId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty)
        {
            logger.LogWarning("RemoteDesktopIceCandidate: could not resolve deviceId from context.");
            return;
        }

        await remoteDesktopHubContext.Clients.Group(DeviceGroup(deviceId))
            .SendAsync("ReceiveRemoteDesktopIceCandidate", sessionId, candidate, sdpMid, sdpMLineIndex);
    }

    // Grid Monitor Methods

    public async Task SendGridFrame(byte[] frameData)
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty) return;

        var groupName = $"Monitor_{deviceId:N}";
        
        // Use the injected context for RemoteDesktopHub to send to viewers
        await remoteDesktopHubContext.Clients.Group(groupName).SendAsync("GridFrameReceived", deviceId, frameData);
    }

    public async Task UploadJobResult(JobResultDto result)

    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty)
        {
            logger.LogWarning("UploadJobResult called without valid DeviceId. ConnectionId: {ConnectionId}", Context.ConnectionId);
            return;
        }

        var job = await dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == result.JobId && j.DeviceId == deviceId);

        if (job == null)
        {
            logger.LogWarning("Job result received for non-existent job. JobId: {JobId}, DeviceId: {DeviceId}", result.JobId, deviceId);
            return;
        }

        job.Complete(result.ExitCode, result.Output);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Job {JobId} completed. ExitCode: {ExitCode}", result.JobId, result.ExitCode);
    }

    public async Task Heartbeat()
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty) return;

        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device != null)
        {
            var ipAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            device.RecordHeartbeat(ipAddress);
            await dbContext.SaveChangesAsync();
        }
    }

    private Guid GetDeviceIdFromContext()
    {
        var deviceIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(deviceIdClaim, out var deviceId) ? deviceId : Guid.Empty;
    }
}