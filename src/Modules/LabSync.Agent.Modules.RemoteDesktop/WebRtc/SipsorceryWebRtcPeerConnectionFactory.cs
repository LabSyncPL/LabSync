using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.WebRtc;

public sealed class SipsorceryWebRtcPeerConnectionFactory : IWebRtcPeerConnectionFactory
{
    private readonly ILogger _logger;

    public SipsorceryWebRtcPeerConnectionFactory(ILogger logger)
    {
        _logger = logger;
    }

    public IWebRtcPeerConnectionService Create(Guid sessionId)
    {
        return new SipsorceryWebRtcPeerConnectionService(sessionId, _logger, new H264RtpPacketizer());
    }
}

