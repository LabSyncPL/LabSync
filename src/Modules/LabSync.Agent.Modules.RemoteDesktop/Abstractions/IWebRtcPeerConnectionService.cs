namespace LabSync.Agent.Modules.RemoteDesktop.Abstractions;

public interface IWebRtcPeerConnectionService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CreateOfferAsync(CancellationToken cancellationToken = default);
    Task SetRemoteAnswerAsync(string sdpType, string sdp, CancellationToken cancellationToken = default);
    Task AddIceCandidateAsync(string candidate, string? sdpMid, int? sdpMLineIndex, CancellationToken cancellationToken = default);
    Task AddVideoTrackFromEncodedStreamAsync(IAsyncEnumerable<EncodedFrame> stream, CancellationToken cancellationToken = default);
    Task<Stream> OpenDataChannelAsync(string label, CancellationToken cancellationToken = default);
    string GetLocalSdpOffer();
    event EventHandler<string>? OnIceCandidate;
    event EventHandler? OnConnectionStateChanged;
}
