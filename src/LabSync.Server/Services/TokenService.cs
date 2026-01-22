using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LabSync.Core.Entities;
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
            // Pobierz wartości z konfiguracji (które już zostały nadpisane z .env)
            _jwtKey = configuration["Jwt:Key"]
                ?? throw new ArgumentNullException("Jwt:Key is not configured");

            _jwtIssuer = configuration["Jwt:Issuer"] ?? "LabSyncServer";
            _jwtAudience = configuration["Jwt:Audience"] ?? "LabSyncAgent";
        }

        public string GenerateAgentToken(Device device)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, device.Id.ToString()),
                new Claim("mac_address", device.MacAddress),
                new Claim("role", "Agent")
            };

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddYears(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}