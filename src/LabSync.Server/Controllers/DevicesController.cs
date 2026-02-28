using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdminRole")]
public class DevicesController(
    LabSyncDbContext context,
    JobDispatchService jobDispatch,
    ILogger<DevicesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceDto>>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await context.Devices
            .Include(d => d.Group)
            .OrderByDescending(d => d.RegisteredAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var devices = entities.Select(d => new DeviceDto
        {
            Id = d.Id,
            Hostname     = d.Hostname,
            IsApproved   = d.IsApproved,
            MacAddress   = d.MacAddress,
            IpAddress    = d.IpAddress,
            Platform     = d.Platform,
            OsVersion    = d.OsVersion,
            Status       = d.Status,
            RegisteredAt = d.RegisteredAt,
            LastSeenAt   = d.LastSeenAt,
            IsOnline     = d.IsOnline,
            GroupId      = d.GroupId,
            GroupName    = d.Group?.Name
        }).ToList();

        return Ok(devices);
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<ApiResponse>> ApproveDevice(Guid id, CancellationToken cancellationToken)
    {
        var device = await context.Devices.FindAsync([id], cancellationToken);
        if (device is null)
            return NotFound(new ApiResponse("Device not found."));

        if (device.IsApproved)
            return Ok(new ApiResponse("Device was already approved."));

        device.Approve();
        await context.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse("Device approved successfully."));
    }

    [HttpPost("{id}/jobs")]
    public async Task<ActionResult<JobDto>> CreateJob(Guid id, [FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var job = await jobDispatch.DispatchAsync(id, request.Command, request.Arguments, request.ScriptPayload, cancellationToken);
        if (job is null)
            return NotFound(new ApiResponse("Device not found or not approved."));

        return AcceptedAtAction(nameof(GetJob), new { deviceId = id, jobId = job.Id }, new JobDto
        {
            Id = job.Id,
            DeviceId   = job.DeviceId,
            Command    = job.Command,
            Arguments  = job.Arguments,
            Status     = job.Status,
            ExitCode   = job.ExitCode,
            Output     = job.Output,
            CreatedAt  = job.CreatedAt,
            FinishedAt = job.FinishedAt
        });
    }

    [HttpGet("{deviceId}/jobs")]
    public async Task<ActionResult<IEnumerable<JobDto>>> GetDeviceJobs(Guid deviceId, CancellationToken cancellationToken)
    {
        var jobs = await context.Jobs
            .AsNoTracking()
            .Where(j => j.DeviceId == deviceId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobDto
            {
                Id = j.Id,
                DeviceId   = j.DeviceId,
                Command    = j.Command,
                Arguments  = j.Arguments,
                Status     = j.Status,
                ExitCode   = j.ExitCode,
                Output     = j.Output,
                CreatedAt  = j.CreatedAt,
                FinishedAt = j.FinishedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(jobs);
    }

    [HttpGet("{deviceId}/jobs/{jobId}")]
    public async Task<ActionResult<JobDto>> GetJob(Guid deviceId, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.DeviceId == deviceId, cancellationToken);

        if (job is null)
            return NotFound(new ApiResponse("Job not found."));

        return Ok(new JobDto
        {
            Id = job.Id,
            DeviceId   = job.DeviceId,
            Command    = job.Command,
            Arguments  = job.Arguments,
            Status     = job.Status,
            ExitCode   = job.ExitCode,
            Output     = job.Output,
            CreatedAt  = job.CreatedAt,
            FinishedAt = job.FinishedAt
        });
    }
}