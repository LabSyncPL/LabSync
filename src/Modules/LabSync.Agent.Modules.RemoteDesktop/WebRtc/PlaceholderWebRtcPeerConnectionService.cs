using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.WebRtc;

public class PlaceholderWebRtcPeerConnectionService : IWebRtcPeerConnectionService
{
    private readonly IRemoteDesktopSignalingService _signalingService;
    private readonly Guid _sessionId;
    private readonly ILogger _logger;
    private string _localSdp = string.Empty;

    public PlaceholderWebRtcPeerConnectionService(
        IRemoteDesktopSignalingService signalingService,
        Guid sessionId,
        ILogger logger)
    {
        _signalingService = signalingService;
        _sessionId = sessionId;
        _logger = logger;
    }

    public event EventHandler<string>? OnIceCandidate;
    public event EventHandler? OnConnectionStateChanged { add { } remove { } }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CreateOfferAsync(CancellationToken cancellationToken = default)
    {
        _localSdp = "v=0\r\n";
        OnIceCandidate?.Invoke(this, "candidate:placeholder");
        return Task.CompletedTask;
    }

    public Task SetRemoteAnswerAsync(string sdpType, string sdp, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task AddIceCandidateAsync(string candidate, string? sdpMid, int? sdpMLineIndex, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task AddVideoTrackFromEncodedStreamAsync(IAsyncEnumerable<EncodedFrame> stream, CancellationToken cancellationToken = default)
    {
        await foreach (var _ in stream.WithCancellation(cancellationToken))
        {
        }
    }

    public Task<Stream> OpenDataChannelAsync(string label, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public string GetLocalSdpOffer() => _localSdp;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
