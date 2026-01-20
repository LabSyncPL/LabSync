using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Core.ValueObjects;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgentsController : ControllerBase
    {
        private readonly LabSyncDbContext _context;
        private readonly TokenService _tokenService;
        private readonly ILogger<AgentsController> _logger;

        public AgentsController(
            LabSyncDbContext context,
            TokenService tokenService,
            ILogger<AgentsController> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new Agent or updates an existing one (Heartbeat/Re-install).
        /// Returns a JWT token for future authentication.
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<RegisterAgentResponse>> Register([FromBody] RegisterAgentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation("Registering agent: {Hostname} ({Mac})", request.Hostname, request.MacAddress);

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.MacAddress == request.MacAddress);

            if (device == null)
            {
                device = new Device
                {
                    Id = Guid.NewGuid(),
                    MacAddress   = request.MacAddress,
                    Hostname     = request.Hostname,
                    IsApproved   = false,
                    Platform     = request.Platform,
                    OsVersion    = request.OsVersion,
                    IpAddress    = request.IpAddress,
                    Status       = DeviceStatus.Pending, 
                    RegisteredAt = DateTime.UtcNow,
                    LastSeenAt   = DateTime.UtcNow
                };

                _context.Devices.Add(device);
            }
            else
            {
                device.Hostname   = request.Hostname;
                device.OsVersion  = request.OsVersion;
                device.IpAddress  = request.IpAddress;
                device.LastSeenAt = DateTime.UtcNow;

                _context.Devices.Update(device);
            }

            if (!device.IsApproved)
            {
                return Ok(new RegisterAgentResponse
                {
                    Token = null, 
                    Message = "Device pending approval."
                });
            }
            var token = _tokenService.GenerateAgentToken(device);
            return Ok(new RegisterAgentResponse { DeviceId = device.Id, Token = token, Message = "Authorized" });
        }
    }
}