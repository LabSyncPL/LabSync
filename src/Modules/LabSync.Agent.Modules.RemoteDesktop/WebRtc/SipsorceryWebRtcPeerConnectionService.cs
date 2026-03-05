using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace LabSync.Agent.Modules.RemoteDesktop.WebRtc;

public sealed class SipsorceryWebRtcPeerConnectionService : IWebRtcPeerConnectionService
{
    private readonly Guid _sessionId;
    private readonly ILogger _logger;
    private readonly Action? _onConnectionClosed;
    private RTCPeerConnection? _pc;
    private RTCDataChannel? _dataChannel;
    private MediaStreamTrack? _videoTrack;
    private uint _videoSsrc;
    private int _videoPayloadType = 96;
    private readonly object _gate = new();
    private bool _disposed;

    private long _startTimestamp = 0;
    private uint _currentRtpTimestamp = 0;
    private bool _isNewFrame = true;

    private byte[]? _lastSps;
    private byte[]? _lastPps;

    private const string StunUrl = "stun:stun.l.google.com:19302";

    public event EventHandler<string>? OnIceCandidate;
    public event EventHandler? OnConnectionStateChanged;

    public SipsorceryWebRtcPeerConnectionService(
        Guid sessionId,
        ILogger logger,
        Action? onConnectionClosed = null)
    {
        _sessionId = sessionId;
        _logger = logger;
        _onConnectionClosed = onConnectionClosed;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var iceServers = new List<RTCIceServer>
        {
            new RTCIceServer { urls = StunUrl },
            
            new RTCIceServer { 
                urls = "turn:global.relay.metered.ca:80",
                username = "aeed81555c66a425b8afe100",
                credential = "dNYLQS6x2lC7MGS3",
            },
            new RTCIceServer { 
                urls = "turn:global.relay.metered.ca:443",
                username = "aeed81555c66a425b8afe100",
                credential = "dNYLQS6x2lC7MGS3",
            },
            new RTCIceServer { 
                urls = "turns:global.relay.metered.ca:443?transport=tcp",
                username = "aeed81555c66a425b8afe100",
                credential = "dNYLQS6x2lC7MGS3",
            }
        };

        var config = new RTCConfiguration
        {
            iceServers = iceServers
        };
        _pc = new RTCPeerConnection(config);

        var videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, 
            new List<SDPAudioVideoMediaFormat> 
            { 
                // Using 0 for channels (5th argument) as required by SIPSorcery constructor
                // FFmpeg is outputting High Profile (42e01f) by default now that we removed -profile:v baseline
                new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, _videoPayloadType, "H264", 90000, 0, "packetization-mode=1;profile-level-id=42e01f") 
            }, 
            MediaStreamStatusEnum.SendRecv);
        
        _pc.addTrack(videoTrack);
        _videoTrack = videoTrack;
        _videoSsrc = videoTrack.Ssrc;

        _pc.onicecandidate += (candidate) =>
        {
            if (candidate == null) return;

            var candidateDict = new Dictionary<string, object>
            {
                { "candidate", candidate.candidate }, 
                { "sdpMid", candidate.sdpMid ?? "" },
                { "sdpMLineIndex", candidate.sdpMLineIndex }
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(candidateDict);
            OnIceCandidate?.Invoke(this, json);
        };

        _pc.onconnectionstatechange += (state) =>
        {
            if (OnConnectionStateChanged != null) OnConnectionStateChanged(this, EventArgs.Empty);
            if (state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.closed)
                _onConnectionClosed?.Invoke();
        };

        await Task.CompletedTask;
    }

    public async Task CreateOfferAsync(CancellationToken cancellationToken = default)
    {
        if (_pc == null) throw new InvalidOperationException("Peer connection not initialized.");
        var offer = _pc.createOffer(null);
        await _pc.setLocalDescription(offer);
    }

    private readonly Queue<RTCIceCandidateInit> _pendingCandidates = new();

    public Task SetRemoteAnswerAsync(string sdpType, string sdp, CancellationToken cancellationToken = default)
    {
        var pc = _pc;
        if (pc == null)
            throw new InvalidOperationException("Peer connection not initialized.");

        var init = new RTCSessionDescriptionInit
        {
            type = sdpType.ToLowerInvariant() == "answer" ? RTCSdpType.answer : RTCSdpType.pranswer,
            sdp = sdp
        };
        var result = pc.setRemoteDescription(init);
        if (result != SetDescriptionResultEnum.OK)
        {
            _logger.LogWarning("setRemoteDescription failed: {Result}. SDP: {Sdp}", result, sdp);
            throw new InvalidOperationException($"setRemoteDescription failed: {result}");
        }

        lock (_pendingCandidates)
        {
            while (_pendingCandidates.Count > 0)
            {
                var cand = _pendingCandidates.Dequeue();
                try 
                {
                    pc.addIceCandidate(cand);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add queued ICE candidate.");
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task AddIceCandidateAsync(string candidate, string? sdpMid, int? sdpMLineIndex, CancellationToken cancellationToken = default)
    {
        var pc = _pc;
        if (pc == null)
            return Task.CompletedTask;

        var init = new RTCIceCandidateInit
        {
            candidate = candidate,
            sdpMid = sdpMid ?? "",
            sdpMLineIndex = (ushort)(sdpMLineIndex ?? 0)
        };

        if (pc.remoteDescription == null)
        {
            lock (_pendingCandidates)
            {
                _pendingCandidates.Enqueue(init);
            }
        }
        else
        {
            pc.addIceCandidate(init);
        }

        return Task.CompletedTask;
    }

    public async Task AddVideoTrackFromEncodedStreamAsync(IAsyncEnumerable<EncodedFrame> stream, CancellationToken cancellationToken = default)
    {
        var pc = _pc;
        if (pc == null || _videoTrack == null) return;

        await foreach (var frame in stream.WithCancellation(cancellationToken))
        {
            int nalType = frame.Data[0] & 0x1F;

            if (nalType == 7) _lastSps = frame.Data;
            if (nalType == 8) _lastPps = frame.Data;

            if (pc.connectionState == RTCPeerConnectionState.connected)
            {
                if (_isNewFrame)
                {
                    long currentTicks = DateTime.UtcNow.Ticks;
                    if (_startTimestamp == 0) _startTimestamp = currentTicks;
                    _currentRtpTimestamp = (uint)((currentTicks - _startTimestamp) * 90000 / 10000000);
                    _isNewFrame = false;
                }

                if (nalType == 5)
                {
                    if (_lastSps != null) pc.SendRtpRaw(SDPMediaTypesEnum.video, _lastSps, _currentRtpTimestamp, 0, _videoPayloadType);
                    if (_lastPps != null) pc.SendRtpRaw(SDPMediaTypesEnum.video, _lastPps, _currentRtpTimestamp, 0, _videoPayloadType);
                }

                if (frame.Data.Length > 1200)
                {
                    await SendFragmentedH264Async(pc, _videoSsrc, _currentRtpTimestamp, frame.Data, nalType);
                }
                else
                {
                    int markerBit = (nalType == 1 || nalType == 5) ? 1 : 0;
                    pc.SendRtpRaw(SDPMediaTypesEnum.video, frame.Data, _currentRtpTimestamp, markerBit, _videoPayloadType);
                }

                if (nalType == 1 || nalType == 5)
                {
                    _isNewFrame = true;
                }
            }
        }
    }

    private async Task SendFragmentedH264Async(RTCPeerConnection pc, uint ssrc, uint timestamp, byte[] nalUnit, int originalNalType)
    {
        const int MTU = 1200;
        byte nalHeader = nalUnit[0];
        int nalType = nalHeader & 0x1F;
        int nri = nalHeader & 0x60;
        int offset = 1;
        int remaining = nalUnit.Length - 1;
        bool isFirst = true;
        int packetsSent = 0;

        while (remaining > 0)
        {
            int len = Math.Min(remaining, MTU);
            bool isLast = (remaining - len) == 0;

            byte fuIndicator = (byte)(nri | 28);
            byte fuHeader = (byte)((isFirst ? 0x80 : 0x00) | (isLast ? 0x40 : 0x00) | nalType);

            var payload = new byte[2 + len];
            payload[0] = fuIndicator;
            payload[1] = fuHeader;
            Buffer.BlockCopy(nalUnit, offset, payload, 2, len);

            int markerBit = (isLast && (originalNalType == 1 || originalNalType == 5)) ? 1 : 0;

            pc.SendRtpRaw(SDPMediaTypesEnum.video, payload, timestamp, markerBit, _videoPayloadType);

            offset += len;
            remaining -= len;
            isFirst = false;

            packetsSent++;
            if (packetsSent % 30 == 0)
            {
                await Task.Yield();
            }
        }
    }

    public async Task<Stream> OpenDataChannelAsync(string label, CancellationToken cancellationToken = default)
    {
        var pc = _pc;
        if (pc == null)
            throw new InvalidOperationException("Peer connection not initialized.");

        _dataChannel = await pc.createDataChannel(label, null);
        return new RtcDataChannelStream(_dataChannel);
    }

    public string GetLocalSdpOffer()
    {
        var pc = _pc;
        if (pc?.currentLocalDescription == null)
            return string.Empty;
        return pc.currentLocalDescription?.sdp?.ToString() ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try
        {
            _pc?.close();
            _pc?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing RTCPeerConnection for session {SessionId}.", _sessionId);
        }

        await Task.CompletedTask;
    }

    private sealed class RtcDataChannelStream : Stream
    {
        private readonly RTCDataChannel _channel;
        private readonly Queue<byte[]> _received = new();
        private readonly SemaphoreSlim _signal = new(0);
        private bool _closed;

        public RtcDataChannelStream(RTCDataChannel channel)
        {
            _channel = channel;
            _channel.onmessage += (_, __, data) =>
            {
                lock (_received) _received.Enqueue(data);
                _signal.Release();
            };
            _channel.onclose += () =>
            {
                _closed = true;
                _signal.Release();
            };
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Use ReadAsync for RTC data channel stream.");

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            while (!_closed)
            {
                byte[]? chunk;
                lock (_received)
                {
                    if (_received.Count > 0)
                    {
                        chunk = _received.Dequeue();
                        var toCopy = Math.Min(count, chunk.Length);
                        Buffer.BlockCopy(chunk, 0, buffer, offset, toCopy);
                        return toCopy;
                    }
                }
                await _signal.WaitAsync(cancellationToken);
            }
            return 0;
        }
    }
}