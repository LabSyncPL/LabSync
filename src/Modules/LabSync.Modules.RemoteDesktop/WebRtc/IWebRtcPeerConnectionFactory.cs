using LabSync.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Modules.RemoteDesktop.WebRtc;

public interface IWebRtcPeerConnectionFactory
{
    IWebRtcPeerConnectionService Create(Guid sessionId);
}
