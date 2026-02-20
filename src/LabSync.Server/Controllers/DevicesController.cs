using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Core.ValueObjects;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "RequireAdminRole")]
    public class DevicesController : ControllerBase
    {
        private readonly LabSyncDbContext _context;
        private readonly JobDispatchService _jobDispatch;
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(LabSyncDbContext context, JobDispatchService jobDispatch, ILogger<DevicesController> logger)
        {
            _context = context;
            _jobDispatch = jobDispatch;
            _logger = logger;
        }

        /// <summary>
        /// Gets a list of all registered devices with their current status and group info.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DeviceDto>>> GetAll()
        {
            _logger.LogInformation("User '{User}' is fetching all devices.", User.Identity?.Name);
            try
            {
                var entities = await _context.Devices
                    .Include(d => d.Group)
                    .OrderByDescending(d => d.RegisteredAt)
                    .ToListAsync();

                var devices = entities.Select(d => new DeviceDto
                {
                    Id = d.Id,
                    Hostname = d.Hostname,
                    IsApproved = d.IsApproved,
                    MacAddress = d.MacAddress,
                    IpAddress = d.IpAddress,
                    Platform = d.Platform,
                    OsVersion = d.OsVersion,
                    Status = d.Status,
                    RegisteredAt = d.RegisteredAt,
                    LastSeenAt = d.LastSeenAt,
                    IsOnline = d.IsOnline,
                    GroupId = d.GroupId,
                    GroupName = d.Group?.Name,
                    HardwareInfo = d.HardwareInfo?.RootElement.ToString()
                }).ToList();

                _logger.LogInformation("Successfully fetched {DeviceCount} devices.", devices.Count);
                return Ok(devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while fetching devices.");
                return StatusCode(500, new { Message = "An internal server error occurred." });
            }
        }

        /// <summary>
        /// Approves a device, allowing it to be authorized.
        /// </summary>
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<ApiResponse>> ApproveDevice(Guid id)
        {
            _logger.LogInformation("Attempting to approve device with ID: {DeviceId}", id);
            var device = await _context.Devices.FindAsync(id);
            
            if (device == null)
            {
                _logger.LogWarning("Approve failed: Device with ID {DeviceId} not found.", id);
                return NotFound(new ApiResponse("Device not found."));
            }

            if (device.IsApproved)
            {
                _logger.LogInformation("Device {DeviceId} is already approved. No action taken.", id);
                return Ok(new ApiResponse("Device was already approved."));
            }

            device.IsApproved = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Device {DeviceId} has been successfully approved.", id);
            return Ok(new ApiResponse("Device approved successfully."));
        }

        /// <summary>
        /// Creates a job for the device and dispatches it via SignalR if the device is online.
        /// </summary>
        [HttpPost("{id}/jobs")]
        public async Task<ActionResult<JobDto>> CreateJob(Guid id, [FromBody] CreateJobRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var job = await _jobDispatch.DispatchAsync(id, request.Command, request.Arguments, request.ScriptPayload, cancellationToken);
            if (job == null)
                return NotFound(new ApiResponse("Device not found or not approved."));

            return AcceptedAtAction(nameof(GetJob), new { deviceId = id, jobId = job.Id }, new JobDto
            {
                Id = job.Id,
                DeviceId = job.DeviceId,
                Command = job.Command,
                Arguments = job.Arguments,
                Status = job.Status,
                ExitCode = job.ExitCode,
                Output = job.Output,
                CreatedAt = job.CreatedAt,
                FinishedAt = job.FinishedAt
            });
        }

        /// <summary>
        /// Gets a specific job for a device.
        /// </summary>
        [HttpGet("{deviceId}/jobs/{jobId}")]
        public async Task<ActionResult<JobDto>> GetJob(Guid deviceId, Guid jobId)
        {
            var job = await _context.Jobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId && j.DeviceId == deviceId);
            if (job == null)
                return NotFound(new ApiResponse("Job not found."));

            return Ok(new JobDto
            {
                Id = job.Id,
                DeviceId = job.DeviceId,
                Command = job.Command,
                Arguments = job.Arguments,
                Status = job.Status,
                ExitCode = job.ExitCode,
                Output = job.Output,
                CreatedAt = job.CreatedAt,
                FinishedAt = job.FinishedAt
            });
        }
    }
}