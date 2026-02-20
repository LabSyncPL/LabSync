using LabSync.Core.Interfaces;
using LabSync.Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace LabSync.Server.Authentication;

public class DeviceKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    LabSyncDbContext dbContext,
    ICryptoService cryptoService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceKey";
    private const string DeviceKeyHeaderName = "x-device-key";
    private const string AccessTokenQueryParam = "access_token";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Try to get the key from header or query string
        if (!Context.Request.Headers.TryGetValue(DeviceKeyHeaderName, out var deviceKey) &&
            !Context.Request.Query.TryGetValue(AccessTokenQueryParam, out deviceKey))
        {
            return AuthenticateResult.NoResult();
        }

        if (string.IsNullOrEmpty(deviceKey))
        {
            return AuthenticateResult.Fail("Device key is missing.");
        }

        // 2. Hash the key
        var deviceKeyHash = cryptoService.Hash(deviceKey.ToString());

        // 3. Find the device in the database
        var device = await dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceKeyHash == deviceKeyHash);

        if (device == null)
        {
            Logger.LogWarning("Authentication failed: Device with the provided key hash not found.");
            return AuthenticateResult.Fail("Invalid device key.");
        }

        if (device.Status == Core.ValueObjects.DeviceStatus.Blocked)
        {
            Logger.LogWarning("Authentication failed: Device {DeviceId} is blocked.", device.Id);
            return AuthenticateResult.Fail("Device is blocked.");
        }

        // 4. Create claims principal
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new Claim(ClaimTypes.Role, "Agent")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("Device {DeviceId} authenticated successfully.", device.Id);
        return AuthenticateResult.Success(ticket);
    }
}
