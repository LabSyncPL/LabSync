using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Agent.Modules.RemoteDesktop.Capture;
using LabSync.Agent.Modules.RemoteDesktop.Encoding;
using LabSync.Agent.Modules.RemoteDesktop.Input;
using LabSync.Agent.Modules.RemoteDesktop.WebRtc;
using LabSync.Core.Dto;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Services;

public class RemoteSessionManager : IRemoteSessionManager
{
    private readonly IRemoteDesktopSignalingService _signalingService;
    private readonly IScreenCaptureFactory _captureFactory;
    private readonly IInputInjectionFactory _inputFactory;
    private readonly ILogger<RemoteSessionManager> _logger;
    private readonly Dictionary<Guid, ActiveSessionContext> _sessions = new();
    private readonly object _gate = new();

    private static readonly TimeSpan SignalingAnswerTimeout = TimeSpan.FromSeconds(30);

    public RemoteSessionManager(
        IRemoteDesktopSignalingService signalingService,
        IScreenCaptureFactory captureFactory,
        IInputInjectionFactory inputFactory,
        ILogger<RemoteSessionManager> logger)
    {
        _signalingService = signalingService;
        _captureFactory = captureFactory;
        _inputFactory = inputFactory;
        _logger = logger;
    }

    public async Task<RemoteSessionResult> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid();
        IScreenCaptureService? capture = null;
        IVideoEncoder? encoder = null;
        IWebRtcPeerConnectionService? peer = null;
        IInputInjectionService? input = null;

        try
        {
            capture = _captureFactory.Create();
            await capture.StartCaptureAsync(cancellationToken);

            encoder = await CreateEncoderAsync(capture, cancellationToken);
            peer = await CreatePeerConnectionAsync(sessionId, encoder, cancellationToken);

            _signalingService.SubscribeToIceCandidates(sessionId, async c =>
            {
                if (peer != null)
                    await peer.AddIceCandidateAsync(c.Candidate, c.SdpMid, c.SdpMLineIndex, CancellationToken.None);
            });

            await peer.CreateOfferAsync(cancellationToken);
            var offer = new RemoteDesktopOfferDto(sessionId, request.DeviceId, "offer", peer.GetLocalSdpOffer());
            await _signalingService.SendOfferAsync(offer, cancellationToken);

            var answerTask = _signalingService.WaitForAnswerAsync(sessionId, SignalingAnswerTimeout, cancellationToken);
            var answer = await answerTask;
            if (answer == null)
            {
                _logger.LogWarning("Remote desktop session {SessionId}: no answer received within timeout.", sessionId);
                return new RemoteSessionResult(false, null, "Signaling timeout: no answer received.");
            }

            await peer.SetRemoteAnswerAsync(answer.SdpType, answer.Sdp, cancellationToken);

            input = _inputFactory.Create();
            var dataChannelStream = await peer.OpenDataChannelAsync("input", cancellationToken);
            _ = RunInputLoopAsync(dataChannelStream, input, cancellationToken);

            await peer.AddVideoTrackFromEncodedStreamAsync(encoder.GetEncodedStreamAsync(cancellationToken), cancellationToken);
            var streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = RunStreamingLoopAsync(capture, encoder, peer, streamCts.Token);

            lock (_gate)
            {
                _sessions[sessionId] = new ActiveSessionContext(
                    streamCts,
                    capture,
                    encoder,
                    peer,
                    input,
                    _signalingService,
                    sessionId);
            }

            _logger.LogInformation("Remote desktop session {SessionId} started for device {DeviceId}.", sessionId, request.DeviceId);
            return new RemoteSessionResult(true, sessionId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start remote desktop session.");
            await DisposeResourcesAsync(capture, encoder, peer, input);
            _signalingService.UnsubscribeFromIceCandidates(sessionId);
            return new RemoteSessionResult(false, null, ex.Message);
        }
    }

    public async Task StopSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        ActiveSessionContext? ctx;
        lock (_gate)
        {
            if (!_sessions.Remove(sessionId, out ctx))
            {
                _logger.LogWarning("StopSession called for unknown session {SessionId}.", sessionId);
                return;
            }
        }

        ctx.StreamCts.Cancel();
        await Task.Delay(500, cancellationToken);

        await DisposeResourcesAsync(ctx.Capture, ctx.Encoder, ctx.Peer, ctx.Input);
        ctx.Signaling.UnsubscribeFromIceCandidates(sessionId);
        _logger.LogInformation("Remote desktop session {SessionId} stopped.", sessionId);
    }

    public bool IsSessionActive(Guid sessionId)
    {
        lock (_gate)
            return _sessions.ContainsKey(sessionId);
    }

    private async Task<IVideoEncoder> CreateEncoderAsync(IScreenCaptureService capture, CancellationToken cancellationToken)
    {
        var encoder = new PlaceholderVideoEncoder(_logger);
        await encoder.InitializeAsync(new EncoderOptions(1920, 1080), cancellationToken);
        return encoder;
    }

    private Task<IWebRtcPeerConnectionService> CreatePeerConnectionAsync(Guid sessionId, IVideoEncoder encoder, CancellationToken cancellationToken)
    {
        var peer = new PlaceholderWebRtcPeerConnectionService(_signalingService, sessionId, _logger);
        peer.OnIceCandidate += (_, candidate) =>
        {
            _ = _signalingService.SendIceCandidateAsync(new IceCandidateDto(sessionId, candidate, null, null), CancellationToken.None);
        };
        return Task.FromResult<IWebRtcPeerConnectionService>(peer);
    }

    private static async Task RunStreamingLoopAsync(IScreenCaptureService capture, IVideoEncoder encoder, IWebRtcPeerConnectionService peer, CancellationToken cancellationToken)
    {
        await foreach (var frame in capture.EnumerateFramesAsync(cancellationToken))
        {
            await encoder.EncodeAsync(frame, cancellationToken);
        }
    }

    private static async Task RunInputLoopAsync(Stream dataChannelStream, IInputInjectionService input, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await dataChannelStream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            await ParseAndInjectInputAsync(buffer.AsSpan(0, read), input, cancellationToken);
        }
    }

    private static Task ParseAndInjectInputAsync(ReadOnlySpan<byte> payload, IInputInjectionService input, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static async Task DisposeResourcesAsync(
        IScreenCaptureService? capture,
        IVideoEncoder? encoder,
        IWebRtcPeerConnectionService? peer,
        IInputInjectionService? input)
    {
        if (encoder != null) await encoder.DisposeAsync();
        if (capture != null) await capture.DisposeAsync();
        if (peer != null) await peer.DisposeAsync();
        if (input != null) await input.DisposeAsync();
    }

    private sealed record ActiveSessionContext(
        CancellationTokenSource StreamCts,
        IScreenCaptureService Capture,
        IVideoEncoder Encoder,
        IWebRtcPeerConnectionService Peer,
        IInputInjectionService Input,
        IRemoteDesktopSignalingService Signaling,
        Guid SessionId
    );
}