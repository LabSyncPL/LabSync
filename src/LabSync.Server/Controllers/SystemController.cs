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
public class SystemController : ControllerBase
{
    private readonly LabSyncDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<SystemController> _logger;

    public SystemController(LabSyncDbContext context, IPasswordHasher passwordHasher, ILogger<SystemController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether the system has been set up (at least one admin exists).
    /// Client uses this to decide: show Setup Wizard or Login.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<SystemStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var hasAnyAdmin = await _context.AdminUsers.AnyAsync(cancellationToken);
        return Ok(new SystemStatusResponse { SetupComplete = hasAnyAdmin });
    }

    /// <summary>
    /// Creates the first administrator account. Allowed only when no admin exists.
    /// After success, further calls return 409 Conflict.
    /// </summary>
    [HttpPost("setup")]
    public async Task<ActionResult<ApiResponse>> Setup([FromBody] SetupRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hasAnyAdmin = await _context.AdminUsers.AnyAsync(cancellationToken);
        if (hasAnyAdmin)
        {
            _logger.LogWarning("Setup rejected: an administrator already exists.");
            return Conflict(new ApiResponse("Setup has already been completed. Use the login page."));
        }

        var existingUsername = await _context.AdminUsers
            .AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (existingUsername)
            return BadRequest(new ApiResponse("This username is not available."));

        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.AdminUsers.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Initial administrator account created. Username: {Username}", admin.Username);
        return Ok(new ApiResponse("Setup complete. You can now log in."));
    }
}
