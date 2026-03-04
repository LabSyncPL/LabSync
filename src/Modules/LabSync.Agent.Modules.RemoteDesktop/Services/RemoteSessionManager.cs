using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Text.Json.Serialization;
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
    private readonly IGpuDiscoveryService _gpuDiscovery;
    private readonly ILogger<RemoteSessionManager> _logger;
    private readonly SessionOptions _options;
    private readonly ConcurrentDictionary<string, RemoteSessionContext> _sessions = new();

    public RemoteSessionManager(
        IRemoteDesktopSignalingService signalingService,
        IScreenCaptureFactory captureFactory,
        IInputInjectionFactory inputFactory,
        IWebRtcPeerConnectionFactory peerFactory,
        IGpuDiscoveryService gpuDiscovery,
        ILogger<RemoteSessionManager> logger,
        SessionOptions? options = null)
    {
        _signalingService = signalingService;
        _captureFactory = captureFactory;
        _inputFactory = inputFactory;
        _peerFactory = peerFactory;
        _gpuDiscovery = gpuDiscovery;
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

            // 1. Initialize Capture
            capture = _captureFactory.Create();
            await capture.StartCaptureAsync(cts.Token);
            
            // Detect Source Resolution (Temporary: Get one frame)
            var enumerator = capture.EnumerateFramesAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            int sourceWidth = 1920;
            int sourceHeight = 1080;
            CaptureFrame? firstFrame = null;
            
            if (await enumerator.MoveNextAsync())
            {
                firstFrame = enumerator.Current;
                sourceWidth = firstFrame.Width;
                sourceHeight = firstFrame.Height;
            }
            
            _logger.LogInformation("Detected screen resolution: {Width}x{Height}", sourceWidth, sourceHeight);

            // 2. Determine Encoder Settings
            var availableEncoders = await _gpuDiscovery.GetAvailableEncodersAsync(cts.Token);
            var preferredEncoderType = VideoEncoderType.Software;
            if (Enum.TryParse<VideoEncoderType>(request.Preferences?.PreferredEncoder, true, out var parsed))
            {
                preferredEncoderType = parsed;
            }

            // Fallback if preferred is not available
            if (!availableEncoders.Contains(preferredEncoderType))
            {
                // Prioritize hardware: Nvidia -> AMD -> Intel -> Software
                if (availableEncoders.Contains(VideoEncoderType.NvidiaNvenc)) preferredEncoderType = VideoEncoderType.NvidiaNvenc;
                else if (availableEncoders.Contains(VideoEncoderType.AmdAmf)) preferredEncoderType = VideoEncoderType.AmdAmf;
                else if (availableEncoders.Contains(VideoEncoderType.IntelQsv)) preferredEncoderType = VideoEncoderType.IntelQsv;
                else preferredEncoderType = VideoEncoderType.Software;
                
                _logger.LogWarning("Preferred encoder not available. Falling back to {Encoder}", preferredEncoderType);
            }

            int targetWidth = request.Preferences?.InitialWidth ?? sourceWidth;
            int targetHeight = request.Preferences?.InitialHeight ?? sourceHeight;
            int fps = request.Preferences?.InitialFps ?? 30;
            int bitrate = request.Preferences?.InitialBitrateKbps ?? 2000;

            // Ensure even dimensions for encoding
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

            encoder = CreateEncoder();
            await encoder.InitializeAsync(encoderOptions, cts.Token);

            // 3. Initialize Peer Connection
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

            // Create Data Channel BEFORE Offer
            var dataChannelStream = await peer.OpenDataChannelAsync("input", cts.Token);

            await peer.CreateOfferAsync(cts.Token);
            var offer = new RemoteDesktopOfferDto(sessionId, request.DeviceId, "offer", peer.GetLocalSdpOffer());
            await _signalingService.SendOfferAsync(offer, cts.Token);

            var answer = await _signalingService.WaitForAnswerAsync(sessionId, _options.OfferTimeout, cts.Token);
            if (answer == null)
            {
                throw new TimeoutException($"No answer received within {_options.OfferTimeout.TotalSeconds}s.");
            }

            await peer.SetRemoteAnswerAsync(answer.SdpType, answer.Sdp, cts.Token);

            input = _inputFactory.Create();

            var captureChannel = Channel.CreateBounded<CaptureFrame>(new BoundedChannelOptions(_options.CaptureChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            var streamTask = peer.AddVideoTrackFromEncodedStreamAsync(encoder.GetEncodedStreamAsync(cts.Token), cts.Token);
            var captureTask = RunCaptureLoopAsync(capture, enumerator, firstFrame, captureChannel.Writer, cts.Token);
            var encodeTask = RunEncodingLoopAsync(captureChannel.Reader, encoder, cts.Token);
            // Pass encoder and options to Input Loop for dynamic updates
            var inputTask = RunInputLoopAsync(dataChannelStream, input, encoder, encoderOptions, cts.Token);

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
            return new WindowsFfmpegVideoEncoder(_logger, _options.EncodedChannelCapacity);
        }
        return new PlaceholderVideoEncoder(_logger, _options.EncodedChannelCapacity);
    }

    private async Task RunCaptureLoopAsync(
        IScreenCaptureService capture,
        IAsyncEnumerator<CaptureFrame> enumerator,
        CaptureFrame? firstFrame,
        ChannelWriter<CaptureFrame> writer,
        CancellationToken cancellationToken)
    {
        int frameCount = 0;
        try
        {
            if (firstFrame != null)
            {
                frameCount++;
                await writer.WriteAsync(firstFrame, cancellationToken);
            }

            while (await enumerator.MoveNextAsync())
            {
                var frame = enumerator.Current;
                if (frameCount++ % 60 == 0) // Log less frequently
                {
                    // _logger.LogDebug(...) 
                }
                await writer.WriteAsync(frame, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Capture loop error */ }
        finally
        {
            await enumerator.DisposeAsync();
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
        catch (Exception) { /* Encoding loop error */ }
    }

    private async Task RunInputLoopAsync(
        Stream dataChannelStream,
        IInputInjectionService input,
        IVideoEncoder encoder,
        EncoderOptions initialOptions,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var currentOptions = initialOptions;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await dataChannelStream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                
                // Parse message
                var json = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                ControlMessage? message = null;
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    options.Converters.Add(new JsonStringEnumConverter());
                    message = System.Text.Json.JsonSerializer.Deserialize<ControlMessage>(json, options);
                }
                catch { /* Ignore malformed JSON */ }

                if (message != null && !string.IsNullOrWhiteSpace(message.Type))
                {
                    if (message.Type.Equals("configure", StringComparison.OrdinalIgnoreCase))
                    {
                        // Handle Configuration
                        int newWidth = message.Width ?? currentOptions.OutputWidth;
                        int newHeight = message.Height ?? currentOptions.OutputHeight;
                        // Ensure even
                        if (newWidth % 2 != 0) newWidth--;
                        if (newHeight % 2 != 0) newHeight--;

                        var newOptions = currentOptions with
                        {
                            OutputWidth = newWidth,
                            OutputHeight = newHeight,
                            TargetBitrateKbps = message.BitrateKbps ?? currentOptions.TargetBitrateKbps,
                            TargetFps = message.Fps ?? currentOptions.TargetFps,
                            EncoderType = message.EncoderType ?? currentOptions.EncoderType
                        };

                        if (newOptions != currentOptions)
                        {
                            await encoder.UpdateSettingsAsync(newOptions, cancellationToken);
                            currentOptions = newOptions;
                        }
                    }
                    else
                    {
                        // Handle Input
                        await InjectInputAsync(message, input, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task InjectInputAsync(ControlMessage message, IInputInjectionService input, CancellationToken cancellationToken)
    {
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

    private sealed class ControlMessage
    {
        public string? Type { get; set; }
        // Input fields
        public int? X { get; set; }
        public int? Y { get; set; }
        public string? Button { get; set; }
        public bool? Pressed { get; set; }
        public int? DeltaX { get; set; }
        public int? DeltaY { get; set; }
        public int? KeyCode { get; set; }
        
        // Configuration fields
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? Fps { get; set; }
        public int? BitrateKbps { get; set; }
        public VideoEncoderType? EncoderType { get; set; }
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
