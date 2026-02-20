using LabSync.Core.Dto;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabSync.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly TokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(TokenService tokenService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _tokenService = tokenService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Authenticates an admin user and returns a JWT for the web panel.
        /// </summary>
        [HttpPost("login")]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUsername = _configuration["Auth:AdminUsername"] ?? "admin";
            var adminPassword = _configuration["Auth:AdminPassword"];

            if (string.IsNullOrEmpty(adminPassword))
            {
                _logger.LogWarning("Auth:AdminPassword is not configured. Rejecting login.");
                return Unauthorized(new ApiResponse("Invalid username or password."));
            }

            if (request.Username != adminUsername || request.Password != adminPassword)
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
}
