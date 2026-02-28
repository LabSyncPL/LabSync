using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Core.Interfaces;
using LabSync.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class SystemController(
    LabSyncDbContext context,
    IPasswordHasher passwordHasher,
    ILogger<SystemController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SystemStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var hasAnyAdmin = await context.AdminUsers.AnyAsync(cancellationToken);
        return Ok(new SystemStatusResponse(hasAnyAdmin));
    }

    [HttpPost("setup")]
    public async Task<ActionResult<ApiResponse>> Setup([FromBody] SetupRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hasAnyAdmin = await context.AdminUsers.AnyAsync(cancellationToken);
        if (hasAnyAdmin)
        {
            return Conflict(new ApiResponse("Setup has already been completed. Use the login page."));
        }

        var admin = new AdminUser(
            request.Username.Trim(),
            passwordHasher.Hash(request.Password)
        );

        context.AdminUsers.Add(admin);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Initial administrator account created. Username: {Username}", admin.Username);
        return Ok(new ApiResponse("Setup complete. You can now log in."));
    }
}