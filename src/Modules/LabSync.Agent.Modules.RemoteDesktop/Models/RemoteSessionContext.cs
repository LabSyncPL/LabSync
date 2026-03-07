using LabSync.Agent.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Agent.Modules.RemoteDesktop.Models;

internal sealed class RemoteSessionContext
{
    public Guid SessionId { get; }
    public IScreenCaptureService? Capture { get; }
    public IVideoEncoder Encoder { get; }
    public IWebRtcPeerConnectionService Peer { get; }
    public IInputInjectionService Input { get; }
    public IRemoteDesktopSignalingService Signaling { get; }
    public CancellationTokenSource LifecycleCts { get; }
    public SessionState State { get; set; }
    public DateTime CreatedAt { get; }
    public DateTime? OfferSentAt { get; set; }
    public DateTime? AnswerReceivedAt { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public EncoderOptions CurrentEncoderOptions { get; set; }

    public RemoteSessionContext(
        Guid sessionId,
        IScreenCaptureService? capture,
        IVideoEncoder encoder,
        IWebRtcPeerConnectionService peer,
        IInputInjectionService input,
        IRemoteDesktopSignalingService signaling,
        CancellationTokenSource lifecycleCts,
        EncoderOptions initialOptions)
    {
        SessionId = sessionId;
        Capture = capture;
        Encoder = encoder;
        Peer = peer;
        Input = input;
        Signaling = signaling;
        LifecycleCts = lifecycleCts;
        CurrentEncoderOptions = initialOptions;
        State = SessionState.Initializing;
        CreatedAt = DateTime.UtcNow;
        LastActivityAt = DateTime.UtcNow;
    }
}

internal enum SessionState
{
    Initializing,
    Signaling,
    Connecting,
    Connected,
    Disconnecting,
    Disposed
}
