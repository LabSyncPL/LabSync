using LabSync.Agent.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Agent.Modules.RemoteDesktop.WebRtc;

public interface IWebRtcPeerConnectionFactory
{
    IWebRtcPeerConnectionService Create(Guid sessionId);
}
