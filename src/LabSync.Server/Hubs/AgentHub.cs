using LabSync.Core.Dto;
using LabSync.Core.ValueObjects;
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
            logger.LogWarning("Agent connected with invalid or missing device claim. Aborting connection.");
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

            var pendingJobs = await dbContext.Jobs
                .Where(j => j.DeviceId == deviceId && j.Status == JobStatus.Pending)
                .ToListAsync();
            foreach (var job in pendingJobs)
            {
                await Clients.Caller.ReceiveJob(job.Id, job.Command, job.Arguments);
                job.Status = JobStatus.Running;
            }
            if (pendingJobs.Count > 0)
            {
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Dispatched {Count} pending job(s) to device {DeviceId}", pendingJobs.Count, deviceId);
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

    /// <summary>
    /// Called by the agent to report the result of a completed job. Updates the job in the database.
    /// </summary>
    public async Task UploadJobResult(JobResultDto result)
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty) return;

        var job = await dbContext.Jobs
            .Include(j => j.Device)
            .FirstOrDefaultAsync(j => j.Id == result.JobId && j.DeviceId == deviceId);

        if (job == null)
        {
            logger.LogWarning("Agent {DeviceId} reported result for unknown or unauthorized job {JobId}", deviceId, result.JobId);
            return;
        }

        job.Status = result.ExitCode == 0 ? JobStatus.Completed : JobStatus.Failed;
        job.ExitCode = result.ExitCode;
        job.Output = result.Output ?? string.Empty;
        job.FinishedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Job {JobId} completed for device {DeviceId}. ExitCode: {ExitCode}", result.JobId, deviceId, result.ExitCode);
    }

    /// <summary>
    /// Called by the agent periodically to update LastSeenAt and confirm the connection is alive.
    /// </summary>
    public async Task Heartbeat()
    {
        var deviceId = GetDeviceIdFromContext();
        if (deviceId == Guid.Empty) return;

        var device = await dbContext.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.LastSeenAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }

    private Guid GetDeviceIdFromContext()
    {
        var deviceIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(deviceIdClaim, out var deviceId) ? deviceId : Guid.Empty;
    }
}
