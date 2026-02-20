using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LabSync.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LabSync.Server.Services
{
    public class TokenService
    {
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public TokenService(IConfiguration configuration)
        {
            // All JWT settings are expected to be present in the configuration.
            // This ensures the application fails fast if not configured properly.
            _jwtKey = configuration["Jwt:Key"]
                ?? throw new ArgumentNullException(nameof(configuration), "JWT configuration 'Jwt:Key' is missing.");

            _jwtIssuer = configuration["Jwt:Issuer"]
                ?? throw new ArgumentNullException(nameof(configuration), "JWT configuration 'Jwt:Issuer' is missing.");
                
            _jwtAudience = configuration["Jwt:Audience"]
                ?? throw new ArgumentNullException(nameof(configuration), "JWT configuration 'Jwt:Audience' is missing.");
        }

        public string GenerateAgentToken(Device device)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, device.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
                new Claim("mac_address", device.MacAddress),
                new Claim(ClaimTypes.Role, "Agent")
            };

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddYears(1), // Agent tokens are long-lived
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Generates a JWT for an admin user (web panel). Token includes role "Admin" for API authorization.
        /// </summary>
        public string GenerateAdminToken(string username)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}