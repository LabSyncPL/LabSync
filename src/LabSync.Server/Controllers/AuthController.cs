using LabSync.Core.Dto;
using LabSync.Core.Entities;
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

        var token = tokenService.GenerateAdminToken(admin.Username);
        logger.LogInformation("Admin user {Username} logged in successfully.", admin.Username);
        return Ok(new LoginResponse(token, 8 * 3600));
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hasAnyAdmin = await context.AdminUsers.AnyAsync(cancellationToken);
        if (!hasAnyAdmin)
        {
            return StatusCode(503, new ApiResponse("Setup required. Please complete the initial setup first."));
        }

        var username = request.Username.Trim();
        if (username.Length < 2 || username.Length > 100)
            return BadRequest(new ApiResponse("Username must be between 2 and 100 characters."));

        if (request.Password.Length < 6 || request.Password.Length > 200)
            return BadRequest(new ApiResponse("Password must be between 6 and 200 characters."));

        var exists = await context.AdminUsers.AnyAsync(u => u.Username == username, cancellationToken);
        if (exists)
            return Conflict(new ApiResponse("Username is already taken."));

        var admin = new AdminUser(username, passwordHasher.Hash(request.Password));
        context.AdminUsers.Add(admin);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("New administrator account registered: {Username}", username);
        return Ok(new ApiResponse("Account created. You can now sign in."));
    }
}