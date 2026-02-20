using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Core.ValueObjects;
using LabSync.Server.Data;
using LabSync.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Services;

/// <summary>
/// Creates jobs in the database and dispatches them to agents via SignalR.
/// </summary>
public class JobDispatchService
{
    private readonly LabSyncDbContext _context;
    private readonly IHubContext<AgentHub, IAgentClient> _hubContext;
    private readonly ConnectionTracker _connectionTracker;
    private readonly ILogger<JobDispatchService> _logger;

    public JobDispatchService(
        LabSyncDbContext context,
        IHubContext<AgentHub, IAgentClient> hubContext,
        ConnectionTracker connectionTracker,
        ILogger<JobDispatchService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    /// <summary>
    /// Queues a job for a device and sends it via SignalR if the device is online.
    /// </summary>
    /// <returns>The created job, or null if the device was not found or not approved.</returns>
    public async Task<Job?> DispatchAsync(Guid deviceId, string command, string arguments, string? scriptPayload = null, CancellationToken cancellationToken = default)
    {
        var device = await _context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device == null)
        {
            _logger.LogWarning("Cannot dispatch job: device {DeviceId} not found.", deviceId);
            return null;
        }

        if (!device.IsApproved)
        {
            _logger.LogWarning("Cannot dispatch job: device {DeviceId} is not approved.", deviceId);
            return null;
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Command = command,
            Arguments = arguments ?? string.Empty,
            ScriptPayload = scriptPayload,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        var connectionId = _connectionTracker.GetConnectionId(deviceId);
        if (connectionId != null)
        {
            try
            {
                await _hubContext.Clients.Client(connectionId).ReceiveJob(job.Id, job.Command, job.Arguments);
                job.Status = JobStatus.Running;
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Job {JobId} dispatched to device {DeviceId}", job.Id, deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send job {JobId} to device {DeviceId}", job.Id, deviceId);
            }
        }
        else
        {
            _logger.LogInformation("Job {JobId} queued for device {DeviceId} (offline). Will be sent when device connects.", job.Id, deviceId);
        }

        return job;
    }
}
