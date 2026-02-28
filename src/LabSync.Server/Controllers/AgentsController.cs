using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Core.Interfaces;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController(
    LabSyncDbContext context,
    TokenService tokenService,
    ICryptoService cryptoService,
    ILogger<AgentsController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<RegisterAgentResponse>> Register([FromBody] RegisterAgentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        logger.LogInformation("Registration attempt from agent: {Hostname} ({MacAddress})", request.Hostname, request.MacAddress);

        var device = await context.Devices.FirstOrDefaultAsync(d => d.MacAddress == request.MacAddress, cancellationToken);
        if (device is null)
        {
            logger.LogInformation("New device detected. Creating entry for {Hostname}.", request.Hostname);

            device = new Device(request.Hostname, request.MacAddress, request.Platform, request.OsVersion, cryptoService.Hash(request.MacAddress));
            if (!string.IsNullOrEmpty(request.IpAddress))
            {
                device.RecordHeartbeat(request.IpAddress);
            }

            context.Devices.Add(device);
        }
        else
        {
            logger.LogInformation("Existing device {Hostname} re-registering.", request.Hostname);
            device.RecordHeartbeat(request.IpAddress ?? "Unknown");
            context.Devices.Update(device);
        }

        await context.SaveChangesAsync(cancellationToken);

        if (!device.IsApproved)
        {
            logger.LogWarning("Device {Hostname} is not approved. Token will not be issued.", device.Hostname);
            return Ok(new RegisterAgentResponse(device.Id, null, "Device registered successfully but requires administrator approval."));
        }

        logger.LogInformation("Device {Hostname} is approved. Generating token.", device.Hostname);
        var token = tokenService.GenerateAgentToken(device);
        return Ok(new RegisterAgentResponse(device.Id, token, "Device is authorized."));
    }
}