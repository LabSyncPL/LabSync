using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LabSync.Core.Entities;
using Microsoft.IdentityModel.Tokens;

namespace LabSync.Server.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAgentToken(Device device)
        {
            var secretKey = _configuration["Jwt:Key"];
            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, device.Id.ToString()), 
                new Claim("mac_address", device.MacAddress),
                new Claim("role", "Agent")
            };

            var token = new JwtSecurityToken(
                issuer:   _configuration["Jwt:Issuer"]   ?? "LabSyncServer",
                audience: _configuration["Jwt:Audience"] ?? "LabSyncAgent",
                claims: claims,
                expires: DateTime.UtcNow.AddYears(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}