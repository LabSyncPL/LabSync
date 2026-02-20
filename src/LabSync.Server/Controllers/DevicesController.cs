using LabSync.Core.Entities;
using LabSync.Core.ValueObjects;
using LabSync.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
        /// Gets a list of all registered devices.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Device>>> GetAll()
        {
            _logger.LogInformation("Fetching all devices.");
            var devices = await _context.Devices
                .OrderByDescending(d => d.RegisteredAt)
                .ToListAsync();

            return Ok(devices);
        }

        /// <summary>
        /// Approves a device, allowing it to be authorized.
        /// </summary>
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveDevice(Guid id)
        {
            _logger.LogInformation("Attempting to approve device with ID: {DeviceId}", id);
            var device = await _context.Devices.FindAsync(id);
            
            if (device == null)
            {
                _logger.LogWarning("Approve failed: Device with ID {DeviceId} not found.", id);
                return NotFound(new { message = "Device not found." });
            }

            if (device.IsApproved)
            {
                _logger.LogInformation("Device {DeviceId} is already approved. No action taken.", id);
                return Ok(new { message = "Device was already approved." });
            }

            device.IsApproved = true;
            device.Status = DeviceStatus.Active;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Device {DeviceId} has been successfully approved.", id);
            return Ok(new { message = "Device approved successfully." });
        }
    }
}