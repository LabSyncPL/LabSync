using LabSync.Agent.Modules.RemoteDesktop.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Services;

internal sealed class SessionWatchdog
{
    private readonly Func<Guid, Task> _stopSessionAsync;
    private readonly SessionOptions _options;
    private readonly ILogger<SessionWatchdog> _logger;

    public SessionWatchdog(
        Func<Guid, Task> stopSessionAsync,
        SessionOptions options,
        ILogger<SessionWatchdog> logger)
    {
        _stopSessionAsync = stopSessionAsync;
        _options = options;
        _logger = logger;
    }

    public void CheckSession(RemoteSessionContext ctx)
    {
        var now = DateTime.UtcNow;
        if (ctx.State == SessionState.Connected && now - ctx.LastActivityAt > _options.IdleTimeout)
        {
            _logger.LogWarning("Session {SessionId} idle timeout ({Idle}s). Stopping.", ctx.SessionId, _options.IdleTimeout.TotalSeconds);
            _ = _stopSessionAsync(ctx.SessionId);
        }
    }
}
