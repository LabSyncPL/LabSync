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

        public AgentsController(LabSyncDbContext context, TokenService tokenService, ILogger<AgentsController> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new Agent or updates an existing one (e.g., Heartbeat/Re-install).
        /// Returns a JWT token if the agent is approved, otherwise returns a message indicating a pending state.
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<RegisterAgentResponse>> Register([FromBody] RegisterAgentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Registration attempt from agent: {Hostname} ({MacAddress})", request.Hostname, request.MacAddress);

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.MacAddress == request.MacAddress);

            if (device == null)
            {
                _logger.LogInformation("New device detected. Creating entry for {Hostname}.", request.Hostname);
                device = new Device
                {
                    Id = Guid.NewGuid(),
                    MacAddress = request.MacAddress,
                    Hostname = request.Hostname,
                    IsApproved = false, // New devices require manual approval
                    Platform = request.Platform,
                    OsVersion = request.OsVersion,
                    IpAddress = request.IpAddress,
                    Status = DeviceStatus.Pending,
                    RegisteredAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                };
                _context.Devices.Add(device);
            }
            else
            {
                _logger.LogInformation("Existing device {Hostname} re-registering or sending heartbeat.", request.Hostname);
                device.Hostname = request.Hostname;
                device.OsVersion = request.OsVersion;
                device.IpAddress = request.IpAddress;
                device.LastSeenAt = DateTime.UtcNow;
                _context.Devices.Update(device);
            }
            
            await _context.SaveChangesAsync();

            if (!device.IsApproved)
            {
                _logger.LogWarning("Device {Hostname} is not approved. Token will not be issued.", device.Hostname);
                return Ok(new RegisterAgentResponse
                {
                    Token = null,
                    Message = "Device registered successfully but requires administrator approval before it can be fully authorized."
                });
            }

            _logger.LogInformation("Device {Hostname} is approved. Generating token.", device.Hostname);
            var token = _tokenService.GenerateAgentToken(device);
            return Ok(new RegisterAgentResponse { DeviceId = device.Id, Token = token, Message = "Device is authorized." });
        }
    }
}