using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.WebRtc;

public class PlaceholderWebRtcPeerConnectionFactory : IWebRtcPeerConnectionFactory
{
    private readonly IRemoteDesktopSignalingService _signalingService;
    private readonly ILogger _logger;

    public PlaceholderWebRtcPeerConnectionFactory(
        IRemoteDesktopSignalingService signalingService,
        ILogger logger)
    {
        _signalingService = signalingService;
        _logger = logger;
    }

    public IWebRtcPeerConnectionService Create(Guid sessionId)
    {
        return new PlaceholderWebRtcPeerConnectionService(_signalingService, sessionId, _logger);
    }
}
