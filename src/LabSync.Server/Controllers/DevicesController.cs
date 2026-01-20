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

        public DevicesController(LabSyncDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets a list of all registered devices.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Device>>> GetAll()
        {
            var devices = await _context.Devices
                .OrderByDescending(d => d.RegisteredAt)
                .ToListAsync();

            return Ok(devices);
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveDevice(Guid id)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device == null) return NotFound();

            device.IsApproved = true;
            device.Status = DeviceStatus.Active;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Device approved" });
        }
    }


}