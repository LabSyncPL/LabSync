using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController(
    TokenService tokenService,
    LabSyncDbContext context,
    IPasswordHasher passwordHasher,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hasAnyAdmin = await context.AdminUsers.AnyAsync(cancellationToken);
        if (!hasAnyAdmin)
        {
            logger.LogWarning("Login rejected: setup not complete.");
            return StatusCode(503, new ApiResponse("Setup required. Please complete the initial setup first."));
        }

        var admin = await context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (admin is null || !passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(new ApiResponse("Invalid username or password."));
        }

        var token = tokenService.GenerateAdminToken(request.Username);
        logger.LogInformation("Admin user {Username} logged in successfully.", request.Username);
        return Ok(new LoginResponse(token, 8 * 3600));
    }
}