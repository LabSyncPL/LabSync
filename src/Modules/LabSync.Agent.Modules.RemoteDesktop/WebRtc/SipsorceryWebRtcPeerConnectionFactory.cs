using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Agent.Modules.RemoteDesktop.Models;

namespace LabSync.Agent.Modules.RemoteDesktop.WebRtc;

public sealed class SipsorceryWebRtcPeerConnectionFactory : IWebRtcPeerConnectionFactory
{
    private readonly ILogger<SipsorceryWebRtcPeerConnectionService> _logger;
    private readonly IOptions<WebRtcConfiguration> _options;

    public SipsorceryWebRtcPeerConnectionFactory(
        ILogger<SipsorceryWebRtcPeerConnectionService> logger,
        IOptions<WebRtcConfiguration> options)
    {
        _logger = logger;
        _options = options;
    }

    public IWebRtcPeerConnectionService Create(Guid sessionId)
    {
        return new SipsorceryWebRtcPeerConnectionService(sessionId, _logger, new H264RtpPacketizer(), _options);
    }
}

