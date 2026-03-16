using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Text.Json.Serialization;
using LabSync.Modules.RemoteDesktop.Abstractions;
using LabSync.Modules.RemoteDesktop.Capture;
using LabSync.Modules.RemoteDesktop.Encoding;
using LabSync.Modules.RemoteDesktop.Infrastructure;
using LabSync.Modules.RemoteDesktop.Input;
using LabSync.Modules.RemoteDesktop.Models;
using LabSync.Modules.RemoteDesktop.WebRtc;
using LabSync.Core.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Modules.RemoteDesktop.Configuration;

namespace LabSync.Modules.RemoteDesktop.Services;

public class RemoteSessionManager : IRemoteSessionManager
{
    private readonly IRemoteDesktopSignalingService _signalingService;
    private readonly IScreenCaptureFactory _captureFactory;
    private readonly IInputInjectionFactory _inputFactory;
    private readonly IWebRtcPeerConnectionFactory _peerFactory;
    private readonly IGpuDiscoveryService _gpuDiscovery;
    private readonly IVideoEncoderFactory _encoderFactory;
    private readonly ISessionInputHandler _inputHandler;
    private readonly ICaptureSession _captureSession;
    private readonly ILogger<RemoteSessionManager> _logger;
    private readonly RemoteDesktopConfiguration _config;
    private readonly ConcurrentDictionary<string, RemoteSessionContext> _sessions = new();

    public RemoteSessionManager(
        IRemoteDesktopSignalingService signalingService,
        IScreenCaptureFactory captureFactory,
        IInputInjectionFactory inputFactory,
        IWebRtcPeerConnectionFactory peerFactory,
        IGpuDiscoveryService gpuDiscovery,
        IVideoEncoderFactory encoderFactory,
        ISessionInputHandler inputHandler,
        ICaptureSession captureSession,
        ILogger<RemoteSessionManager> logger,
        IOptions<RemoteDesktopConfiguration> options)
    {
        _signalingService = signalingService;
        _captureFactory = captureFactory;
        _inputFactory = inputFactory;
        _peerFactory = peerFactory;
        _gpuDiscovery = gpuDiscovery;
        _encoderFactory = encoderFactory;
        _inputHandler = inputHandler;
        _captureSession = captureSession;
        _logger = logger;
        _config = options.Value;
    }

    public async Task<RemoteSessionResult> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid();
        var key = sessionId.ToString("N");
        IScreenCaptureService? capture = null;
        IVideoEncoder? encoder = null;
        IWebRtcPeerConnectionService? peer = null;
        IInputInjectionService? input = null;
        CancellationTokenSource? lifecycleCts = null;

        try
        {
            lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var cts = lifecycleCts;

            int sourceWidth = 1920;
            int sourceHeight = 1080;
            CaptureFrame? firstFrame = null;
            IAsyncEnumerator<CaptureFrame>? enumerator = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                capture = _captureFactory.Create();
                await capture.StartCaptureAsync(cts.Token);
                
                enumerator = capture.EnumerateFramesAsync(cts.Token).GetAsyncEnumerator(cts.Token);
                if (await enumerator.MoveNextAsync())
                {
                    firstFrame = enumerator.Current;
                    sourceWidth = firstFrame.Width;
                    sourceHeight = firstFrame.Height;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
             {
                 var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
                 _logger.LogInformation("Linux Session Type: {SessionType}", sessionType ?? "Unknown");
                 
                 (sourceWidth, sourceHeight) = await LinuxDisplayHelper.GetScreenResolutionAsync(cts.Token);
             }
            
            _logger.LogInformation("Detected screen resolution: {Width}x{Height}", sourceWidth, sourceHeight);

            var availableEncoders = await _gpuDiscovery.GetAvailableEncodersAsync(cts.Token);
            var preferredEncoderType = VideoEncoderType.Software;
            if (Enum.TryParse<VideoEncoderType>(request.Preferences?.PreferredEncoder, true, out var parsed))
            {
                preferredEncoderType = parsed;
            }

            if (!availableEncoders.Contains(preferredEncoderType))
            {
                if (availableEncoders.Contains(VideoEncoderType.NvidiaNvenc)) preferredEncoderType = VideoEncoderType.NvidiaNvenc;
                else if (availableEncoders.Contains(VideoEncoderType.AmdAmf)) preferredEncoderType = VideoEncoderType.AmdAmf;
                else if (availableEncoders.Contains(VideoEncoderType.IntelQsv)) preferredEncoderType = VideoEncoderType.IntelQsv;
                else preferredEncoderType = VideoEncoderType.Software;
                
                _logger.LogWarning("Preferred encoder not available. Falling back to {Encoder}", preferredEncoderType);
            }

            int targetWidth = request.Preferences?.InitialWidth ?? sourceWidth;
            int targetHeight = request.Preferences?.InitialHeight ?? sourceHeight;
            int fps = request.Preferences?.InitialFps ?? _config.Encoding.DefaultFps;
            int bitrate = request.Preferences?.InitialBitrateKbps ?? _config.Encoding.DefaultBitrateKbps;

            if (targetWidth % 2 != 0) targetWidth--;
            if (targetHeight % 2 != 0) targetHeight--;

            var encoderOptions = new EncoderOptions(
                SourceWidth: sourceWidth,
                SourceHeight: sourceHeight,
                OutputWidth: targetWidth,
                OutputHeight: targetHeight,
                TargetBitrateKbps: bitrate,
                TargetFps: fps,
                EncoderType: preferredEncoderType
            );

            encoder = _encoderFactory.Create();
            await encoder.InitializeAsync(encoderOptions, cts.Token);

            peer = _peerFactory.Create(sessionId);
            peer.OnIceCandidate += (_, candidate) =>
            {
                if (!cts.Token.IsCancellationRequested)
                    _ = SafeSendIceCandidate(sessionId, candidate);
            };

            _signalingService.SubscribeToIceCandidates(sessionId, c =>
            {
                if (!cts.Token.IsCancellationRequested)
                    _ = SafeAddIceCandidate(peer, c, cts.Token);
            });

            await peer.InitializeAsync(cts.Token);

            var dataChannelStream = await peer.OpenDataChannelAsync("input", cts.Token);
            await peer.CreateOfferAsync(cts.Token);
            var offer = new RemoteDesktopOfferDto(
                sessionId,
                request.DeviceId,
                "offer",
                peer.GetLocalSdpOffer(),
                availableEncoders.Select(e => e.ToString()).ToArray());
            await _signalingService.SendOfferAsync(offer, cts.Token);

            _logger.LogInformation("Sent SDP Offer for session {SessionId}.", sessionId);

            var answer = await _signalingService.WaitForAnswerAsync(sessionId, _config.Session.OfferTimeout, cts.Token);
            if (answer == null)
            {
                throw new TimeoutException($"No answer received within {_config.Session.OfferTimeout.TotalSeconds}s.");
            }

            await peer.SetRemoteAnswerAsync(answer.SdpType, answer.Sdp, cts.Token);

            input = _inputFactory.Create();

            var streamTask = peer.AddVideoTrackFromEncodedStreamAsync(encoder.GetEncodedStreamAsync(cts.Token), cts.Token);
            
            var (captureTask, encodeTask) = _captureSession.Start(
                capture,
                enumerator,
                firstFrame,
                encoder,
                cts.Token);
            
            var inputTask = _inputHandler.RunInputLoopAsync(
                dataChannelStream, 
                input, 
                async (opts) => await encoder.UpdateSettingsAsync(opts, cts.Token), 
                encoderOptions, 
                cts.Token);
            var ctx = new RemoteSessionContext(sessionId, capture, encoder, peer, input, _signalingService, lifecycleCts, encoderOptions)
            {
                State = SessionState.Connected,
                OfferSentAt = DateTime.UtcNow,
                AnswerReceivedAt = DateTime.UtcNow,
                ConnectedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            if (!_sessions.TryAdd(key, ctx))
            {
                await RollbackAsync(capture, encoder, peer, input, lifecycleCts, sessionId);
                throw new InvalidOperationException("Session already exists.");
            }

            _ = TrackBackgroundTasks(sessionId, key, captureTask, encodeTask, streamTask, inputTask);

            _logger.LogInformation("Session {SessionId} started. Encoder: {Encoder}, Resolution: {W}x{H}", sessionId, preferredEncoderType, targetWidth, targetHeight);
            return new RemoteSessionResult(true, sessionId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} failed to start.", sessionId);
            await RollbackAsync(capture, encoder, peer, input, lifecycleCts, sessionId);
            _signalingService.UnsubscribeFromIceCandidates(sessionId);
            return new RemoteSessionResult(false, null, ex.Message);
        }
    }

    public async Task StopSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var key = sessionId.ToString("N");
        if (!_sessions.TryRemove(key, out var ctx))
        {
            return;
        }

        ctx.State = SessionState.Disconnecting;
        ctx.LifecycleCts.Cancel();

        try
        {
            await Task.Delay(_config.Session.StopGracePeriod, cancellationToken);
        }
        catch (OperationCanceledException) { }

        await DisposeResourcesAsync(ctx.Capture, ctx.Encoder, ctx.Peer, ctx.Input);
        ctx.Signaling.UnsubscribeFromIceCandidates(sessionId);
        ctx.State = SessionState.Disposed;
        ctx.LifecycleCts.Dispose();
        _logger.LogInformation("Session {SessionId} stopped.", sessionId);
    }

    public bool IsSessionActive(Guid sessionId)
    {
        return _sessions.ContainsKey(sessionId.ToString("N"));
    }











    private async Task TrackBackgroundTasks(Guid sessionId, string key, params Task[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background task failed for session {SessionId}.", sessionId);
            if (_sessions.TryRemove(key, out var ctx))
            {
                ctx.LifecycleCts.Cancel();
                await DisposeResourcesAsync(ctx.Capture, ctx.Encoder, ctx.Peer, ctx.Input);
                ctx.Signaling.UnsubscribeFromIceCandidates(sessionId);
            }
        }
    }

    private async Task SafeSendIceCandidate(Guid sessionId, string candidate)
    {
        try
        {
            string candStr = candidate;
            string? sdpMid = null;
            int? sdpMLineIndex = null;
            try 
            {
                using var doc = System.Text.Json.JsonDocument.Parse(candidate);
                var root = doc.RootElement;
                if (root.TryGetProperty("candidate", out var cProp)) candStr = cProp.GetString() ?? "";
                if (root.TryGetProperty("sdpMid", out var midProp)) sdpMid = midProp.GetString();
                if (root.TryGetProperty("sdpMLineIndex", out var idxProp)) sdpMLineIndex = idxProp.GetInt32();
            }
            catch { }
            await _signalingService.SendIceCandidateAsync(new IceCandidateDto(sessionId, candStr, sdpMid, sdpMLineIndex), CancellationToken.None);
        }
        catch { }
    }

    private static async Task SafeAddIceCandidate(IWebRtcPeerConnectionService peer, IceCandidateDto c, CancellationToken ct)
    {
        try { await peer.AddIceCandidateAsync(c.Candidate, c.SdpMid, c.SdpMLineIndex, ct); } catch { }
    }

    private async Task RollbackAsync(
        IScreenCaptureService? capture,
        IVideoEncoder? encoder,
        IWebRtcPeerConnectionService? peer,
        IInputInjectionService? input,
        CancellationTokenSource? cts,
        Guid sessionId)
    {
        cts?.Cancel();
        try { await Task.Delay(100, CancellationToken.None); } catch { }
        await DisposeResourcesAsync(capture, encoder, peer, input);
        cts?.Dispose();
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
}
