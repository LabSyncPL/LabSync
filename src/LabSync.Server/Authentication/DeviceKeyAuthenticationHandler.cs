using LabSync.Core.Interfaces;
using LabSync.Core.Types;
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
    public  const string SchemeName            = "DeviceKey";
    private const string DeviceKeyHeaderName   = "x-device-key";
    private const string AccessTokenQueryParam = "access_token";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? deviceKey = Request.Headers[DeviceKeyHeaderName];

        if (string.IsNullOrEmpty(deviceKey))
        {
            deviceKey = Request.Query[AccessTokenQueryParam];
        }

        if (string.IsNullOrEmpty(deviceKey))
        {
            Logger.LogWarning("Authentication failed: No device key provided in headers or query parameters.");
            return AuthenticateResult.NoResult();
        }

        var deviceKeyHash = cryptoService.Hash(deviceKey);
        var device = await dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceKeyHash == deviceKeyHash, Context.RequestAborted);

        if (device is null)
        {
            Logger.LogWarning("Authentication failed: Device with the provided key hash not found.");
            return AuthenticateResult.Fail("Invalid device key.");
        }

        if (device.Status == DeviceStatus.Blocked)
        {
            Logger.LogWarning("Authentication failed: Device {DeviceId} is blocked.", device.Id);
            return AuthenticateResult.Fail("Device is blocked.");
        }

        if (!device.IsApproved)
        {
            Logger.LogWarning("Authentication failed: Device {DeviceId} is pending approval.", device.Id);
            return AuthenticateResult.Fail("Device is pending approval.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new Claim(ClaimTypes.Role, "Agent")
        };

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        Logger.LogInformation("Device {DeviceId} authenticated successfully.", device.Id);
        return AuthenticateResult.Success(ticket);
    }
}