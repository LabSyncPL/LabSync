using System.Collections.Concurrent;
using System.Threading.Channels;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Agent.Modules.RemoteDesktop.Capture;
using LabSync.Agent.Modules.RemoteDesktop.Encoding;
using LabSync.Agent.Modules.RemoteDesktop.Input;
using LabSync.Agent.Modules.RemoteDesktop.Models;
using LabSync.Agent.Modules.RemoteDesktop.WebRtc;
using LabSync.Core.Dto;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Services;

public class RemoteSessionManager : IRemoteSessionManager
{
    private readonly IRemoteDesktopSignalingService _signalingService;
    private readonly IScreenCaptureFactory _captureFactory;
    private readonly IInputInjectionFactory _inputFactory;
    private readonly IWebRtcPeerConnectionFactory _peerFactory;
    private readonly ILogger<RemoteSessionManager> _logger;
    private readonly SessionOptions _options;
    private readonly ConcurrentDictionary<string, RemoteSessionContext> _sessions = new();

    public RemoteSessionManager(
        IRemoteDesktopSignalingService signalingService,
        IScreenCaptureFactory captureFactory,
        IInputInjectionFactory inputFactory,
        IWebRtcPeerConnectionFactory peerFactory,
        ILogger<RemoteSessionManager> logger,
        SessionOptions? options = null)
    {
        _signalingService = signalingService;
        _captureFactory = captureFactory;
        _inputFactory = inputFactory;
        _peerFactory = peerFactory;
        _logger = logger;
        _options = options ?? SessionOptions.Default;
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

            capture = _captureFactory.Create();
            await capture.StartCaptureAsync(cts.Token);

            encoder = CreateEncoder();
            await encoder.InitializeAsync(new EncoderOptions(1920, 1080), cts.Token);

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

            // Create Data Channel BEFORE Offer so it's included in SDP
            var dataChannelStream = await peer.OpenDataChannelAsync("input", cts.Token);

            await peer.CreateOfferAsync(cts.Token);
            var offer = new RemoteDesktopOfferDto(sessionId, request.DeviceId, "offer", peer.GetLocalSdpOffer());
            await _signalingService.SendOfferAsync(offer, cts.Token);

            var answer = await _signalingService.WaitForAnswerAsync(sessionId, _options.OfferTimeout, cts.Token);
            if (answer == null)
            {
                _logger.LogWarning("Session {SessionId}: offer timeout - no answer within {Timeout}s.",
                    sessionId, _options.OfferTimeout.TotalSeconds);
                throw new TimeoutException($"No answer received within {_options.OfferTimeout.TotalSeconds}s.");
            }

            await peer.SetRemoteAnswerAsync(answer.SdpType, answer.Sdp, cts.Token);

            input = _inputFactory.Create();
            // Data channel already created

            var captureChannel = Channel.CreateBounded<CaptureFrame>(new BoundedChannelOptions(_options.CaptureChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            var streamTask = peer.AddVideoTrackFromEncodedStreamAsync(encoder.GetEncodedStreamAsync(cts.Token), cts.Token);
            var captureTask = RunCaptureLoopAsync(capture, captureChannel.Writer, cts.Token);
            var encodeTask = RunEncodingLoopAsync(captureChannel.Reader, encoder, cts.Token);
            var inputTask = RunInputLoopAsync(dataChannelStream, input, cts.Token);

            var ctx = new RemoteSessionContext(sessionId, capture, encoder, peer, input, _signalingService, lifecycleCts)
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

            _logger.LogInformation("Session {SessionId} started for device {DeviceId}.", sessionId, request.DeviceId);
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
            _logger.LogWarning("StopSession: unknown session {SessionId}.", sessionId);
            return;
        }

        ctx.State = SessionState.Disconnecting;
        ctx.LifecycleCts.Cancel();

        try
        {
            await Task.Delay(_options.StopGracePeriod, cancellationToken);
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

    private IVideoEncoder CreateEncoder()
    {
        if (OperatingSystem.IsWindows())
        {
            _logger.LogInformation("Using WindowsFfmpegVideoEncoder on Windows.");
            return new WindowsFfmpegVideoEncoder(_logger, _options.EncodedChannelCapacity);
        }

        return new PlaceholderVideoEncoder(_logger, _options.EncodedChannelCapacity);
    }

    private async Task RunCaptureLoopAsync(
        IScreenCaptureService capture,
        ChannelWriter<CaptureFrame> writer,
        CancellationToken cancellationToken)
    {
        int frameCount = 0;
        try
        {
            await foreach (var frame in capture.EnumerateFramesAsync(cancellationToken))
            {
                if (frameCount++ % 30 == 0) _logger.LogDebug("Captured frame {Count}. Size: {Width}x{Height}", frameCount, frame.Width, frame.Height);
                await writer.WriteAsync(frame, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture loop error.");
            throw;
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task RunEncodingLoopAsync(
        ChannelReader<CaptureFrame> reader,
        IVideoEncoder encoder,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in reader.ReadAllAsync(cancellationToken))
            {
                await encoder.EncodeAsync(frame, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encoding loop error.");
            throw;
        }
    }

    private static async Task RunInputLoopAsync(Stream dataChannelStream, IInputInjectionService input, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await dataChannelStream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                await ParseAndInjectInputAsync(buffer, read, input, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task ParseAndInjectInputAsync(byte[] payload, int length, IInputInjectionService input, CancellationToken cancellationToken)
    {
        if (length <= 0)
            return;

        // Simple JSON-based input protocol:
        // { "type": "mouseMove", "x": 100, "y": 200 }
        // { "type": "mouseButton", "button": "left", "pressed": true }
        // { "type": "mouseWheel", "deltaX": 0, "deltaY": -120 }
        // { "type": "key", "keyCode": 65, "pressed": true }

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(payload, 0, length);
            var message = System.Text.Json.JsonSerializer.Deserialize<InputMessage>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (message is null || string.IsNullOrWhiteSpace(message.Type))
                return;

            switch (message.Type)
            {
                case "mouseMove" when message.X is not null && message.Y is not null:
                    await input.InjectMouseMoveAsync(message.X.Value, message.Y.Value, cancellationToken);
                    break;

                case "mouseButton" when message.Button is not null && message.Pressed is not null:
                    if (Enum.TryParse<MouseButton>(message.Button, ignoreCase: true, out var button))
                    {
                        await input.InjectMouseButtonAsync(button, message.Pressed.Value, cancellationToken);
                    }
                    break;

                case "mouseWheel" when message.DeltaX is not null && message.DeltaY is not null:
                    await input.InjectMouseWheelAsync(message.DeltaX.Value, message.DeltaY.Value, cancellationToken);
                    break;

                case "key" when message.KeyCode is not null && message.Pressed is not null:
                    if (message.KeyCode.Value is >= 0 and <= 255)
                    {
                        await input.InjectKeyAsync(message.KeyCode.Value, message.Pressed.Value, cancellationToken);
                    }
                    break;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Ignore malformed input.
        }
    }

    private sealed class InputMessage
    {
        public string? Type { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public string? Button { get; set; }
        public bool? Pressed { get; set; }
        public int? DeltaX { get; set; }
        public int? DeltaY { get; set; }
        public int? KeyCode { get; set; }
    }

    private async Task TrackBackgroundTasks(Guid sessionId, string key, params Task[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background task failed for session {SessionId}. Stopping session.", sessionId);
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
            // Parse candidate JSON to extract sdpMid and sdpMLineIndex if available
            // SipsorceryWebRtcPeerConnectionService now sends a JSON with { candidate, sdpMid, sdpMLineIndex }
            
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
            catch
            {
                // Fallback: assume it's just the candidate string
            }

            await _signalingService.SendIceCandidateAsync(new IceCandidateDto(sessionId, candStr, sdpMid, sdpMLineIndex), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send ICE candidate for session {SessionId}.", sessionId);
        }
    }

    private static async Task SafeAddIceCandidate(IWebRtcPeerConnectionService peer, IceCandidateDto c, CancellationToken ct)
    {
        try
        {
            await peer.AddIceCandidateAsync(c.Candidate, c.SdpMid, c.SdpMLineIndex, ct);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
        catch (Exception) { /* peer may be disposed */ }
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
        try
        {
            await Task.Delay(100, CancellationToken.None);
        }
        catch { }

        if (encoder != null) await encoder.DisposeAsync();
        if (capture != null) await capture.DisposeAsync();
        if (peer != null) await peer.DisposeAsync();
        if (input != null) await input.DisposeAsync();
        cts?.Dispose();
        _logger.LogDebug("Rollback completed for failed session {SessionId}.", sessionId);
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
