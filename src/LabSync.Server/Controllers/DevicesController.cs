﻿﻿﻿﻿﻿﻿using LabSync.Core.Dto;
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
public class DevicesController : ControllerBase
{
    private readonly LabSyncDbContext context;
    private readonly JobDispatchService jobDispatch;
    private readonly ISecretProvider secretProvider;
    private readonly ILogger<DevicesController> logger;

    public DevicesController(
        LabSyncDbContext context,
        JobDispatchService jobDispatch,
        ISecretProvider secretProvider,
        ILogger<DevicesController> logger)
    {
        this.context = context;
        this.jobDispatch = jobDispatch;
        this.secretProvider = secretProvider;
        this.logger = logger;
    }
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceDto>>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await context.Devices
            .Include(d => d.Group)
            .Include(d => d.Credentials)
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
            GroupName    = d.Group?.Name,
            HasSshCredentials = d.Credentials != null,
            UseKeyAuthentication = d.Credentials?.UseKeyAuthentication ?? false
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

    [HttpPost("{id}/credentials")]
    public async Task<ActionResult<ApiResponse>> SetSshCredentials(Guid id, [FromBody] SetSshCredentialsRequest request, CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Include(d => d.Credentials)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            
        if (device is null)
            return NotFound(new ApiResponse("Device not found."));

        string? keyReference = null;
        if (!string.IsNullOrEmpty(request.PrivateKey))
        {
            keyReference = await secretProvider.StoreSecretAsync(request.PrivateKey, $"device:{id}");
        }

        if (device.Credentials != null)
        {
            device.Credentials.SshUsername = request.Username;
            if (!string.IsNullOrEmpty(request.Password))
            {
                device.Credentials.SshPassword = request.Password; // TODO: Encrypt
            }
            if (keyReference != null)
            {
                device.Credentials.SshKeyReference = keyReference;
                device.Credentials.SshPrivateKey = null;
            }
            if (request.UseKeyAuthentication.HasValue)
            {
                device.Credentials.UseKeyAuthentication = request.UseKeyAuthentication.Value;
            }
        }
        else
        {
            var creds = new Core.Entities.DeviceCredentials(id, request.Username, request.Password ?? string.Empty);
            creds.SshKeyReference = keyReference;
            creds.SshPrivateKey = null;
            if (request.UseKeyAuthentication.HasValue)
            {
                creds.UseKeyAuthentication = request.UseKeyAuthentication.Value;
            }
            context.DeviceCredentials.Add(creds);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse("SSH Credentials saved successfully."));
    }
}

public record SetSshCredentialsRequest(string Username, string? Password, string? PrivateKey, bool? UseKeyAuthentication);