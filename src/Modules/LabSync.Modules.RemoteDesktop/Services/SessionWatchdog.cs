using LabSync.Modules.RemoteDesktop.Configuration;
using LabSync.Modules.RemoteDesktop.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabSync.Modules.RemoteDesktop.Services;

internal sealed class SessionWatchdog
{
    private readonly Func<Guid, Task> _stopSessionAsync;
    private readonly RemoteDesktopConfiguration _config;
    private readonly ILogger<SessionWatchdog> _logger;

    public SessionWatchdog(
        Func<Guid, Task> stopSessionAsync,
        IOptions<RemoteDesktopConfiguration> options,
        ILogger<SessionWatchdog> logger)
    {
        _stopSessionAsync = stopSessionAsync;
        _config = options.Value;
        _logger = logger;
    }

    public void CheckSession(RemoteSessionContext ctx)
    {
        var now = DateTime.UtcNow;
        if (ctx.State == SessionState.Connected && now - ctx.LastActivityAt > _config.Session.IdleTimeout)
        {
            _logger.LogWarning("Session {SessionId} idle timeout ({Idle}s). Stopping.", ctx.SessionId, _config.Session.IdleTimeout.TotalSeconds);
            _ = _stopSessionAsync(ctx.SessionId);
        }
    }
}
