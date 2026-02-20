using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Core.ValueObjects;
using LabSync.Server.Data;
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
        private readonly ILogger<DevicesController> _logger;

        public DevicesController(LabSyncDbContext context, ILogger<DevicesController> logger)
        {
            _context = context;
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
    }
}