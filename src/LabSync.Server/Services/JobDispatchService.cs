using LabSync.Core.Entities;
using LabSync.Server.Data;
using LabSync.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Services;


public class JobDispatchService(
    LabSyncDbContext context,
    IHubContext<AgentHub, IAgentClient> hubContext,
    ConnectionTracker _connectionTracker,
    ILogger<JobDispatchService> logger)
{
    public async Task<Job?> DispatchAsync(Guid deviceId, string command, string arguments, string? scriptPayload = null, CancellationToken cancellationToken = default)
    {
        var device = await context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null || !device.IsApproved)
        {
            logger.LogWarning("Cannot dispatch job: device {DeviceId} not found or not approved.", deviceId);
            return null;
        }

        var job = new Job(deviceId, command, arguments, scriptPayload);
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        var connectionId = _connectionTracker.GetConnectionId(deviceId);
        if (connectionId != null)
        {
            try
            {
                await hubContext.Clients.Client(connectionId).ReceiveJob(job.Id, job.Command, job.Arguments, job.ScriptPayload);
                job.MarkAsRunning();

                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Job {JobId} dispatched to device {DeviceId}", job.Id, deviceId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send job {JobId} to device {DeviceId}", job.Id, deviceId);
            }
        }
        else
        {
            logger.LogInformation("Job {JobId} queued for offline device {DeviceId}.", job.Id, deviceId);
        }

        return job;
    }
}