using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
[Authorize(Policy = "RequireAdminRole")]
public class AccountController(
    LabSyncDbContext context,
    IPasswordHasher passwordHasher,
    TokenService tokenService,
    ILogger<AccountController> logger) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<AccountProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username))
            return Unauthorized(new ApiResponse("Not authenticated."));

        var admin = await context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (admin is null)
            return NotFound(new ApiResponse("Account not found."));

        return Ok(new AccountProfileDto(admin.Username, admin.CreatedAt));
    }

    [HttpPatch("password")]
    public async Task<ActionResult<LoginResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.NewPassword.Length < 6 || request.NewPassword.Length > 200)
            return BadRequest(new ApiResponse("New password must be between 6 and 200 characters."));

        var admin = await FindCurrentAdminAsync(cancellationToken);
        if (admin is null)
            return NotFound(new ApiResponse("Account not found."));

        if (!passwordHasher.Verify(request.CurrentPassword, admin.PasswordHash))
            return Unauthorized(new ApiResponse("Current password is incorrect."));

        admin.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin user {Username} changed password.", admin.Username);
        var token = tokenService.GenerateAdminToken(admin.Username);
        return Ok(new LoginResponse(token, 8 * 3600));
    }

    [HttpPatch("username")]
    public async Task<ActionResult<LoginResponse>> ChangeUsername(
        [FromBody] ChangeUsernameRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var newUsername = request.NewUsername.Trim();
        if (newUsername.Length < 2 || newUsername.Length > 100)
            return BadRequest(new ApiResponse("Username must be between 2 and 100 characters."));

        var admin = await FindCurrentAdminAsync(cancellationToken);
        if (admin is null)
            return NotFound(new ApiResponse("Account not found."));

        if (!passwordHasher.Verify(request.CurrentPassword, admin.PasswordHash))
            return Unauthorized(new ApiResponse("Current password is incorrect."));

        if (!string.Equals(admin.Username, newUsername, StringComparison.Ordinal))
        {
            var taken = await context.AdminUsers.AnyAsync(u => u.Username == newUsername, cancellationToken);
            if (taken)
                return Conflict(new ApiResponse("Username is already taken."));
        }

        admin.ChangeUsername(newUsername);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin user renamed to {Username}.", newUsername);
        var token = tokenService.GenerateAdminToken(admin.Username);
        return Ok(new LoginResponse(token, 8 * 3600));
    }

    private string? GetCurrentUsername() =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    private async Task<Core.Entities.AdminUser?> FindCurrentAdminAsync(CancellationToken cancellationToken)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username))
            return null;

        return await context.AdminUsers.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }
}
