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
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly LabSyncDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        TokenService tokenService,
        LabSyncDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates an admin user and returns a JWT for the web panel.
    /// Returns 503 if setup has not been completed (no admin account exists).
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hasAnyAdmin = await _context.AdminUsers.AnyAsync(cancellationToken);
        if (!hasAnyAdmin)
        {
            _logger.LogWarning("Login rejected: setup not complete (no admin account).");
            return StatusCode(503, new ApiResponse("Setup required. Please complete the initial setup first."));
        }

        var admin = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (admin == null || !_passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(new ApiResponse("Invalid username or password."));
        }

        var token = _tokenService.GenerateAdminToken(request.Username);
        const int expiresInSeconds = 8 * 3600; // 8 hours

        _logger.LogInformation("Admin user {Username} logged in successfully.", request.Username);
        return Ok(new LoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresInSeconds = expiresInSeconds
        });
    }
}
