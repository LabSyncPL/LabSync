using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Services;

public class RemoteDesktopSignalingService : IRemoteDesktopSignalingService
{
    private readonly IAgentHubInvoker _hubInvoker;
    private readonly ILogger<RemoteDesktopSignalingService> _logger;
    private readonly Dictionary<Guid, TaskCompletionSource<RemoteDesktopAnswerDto?>> _answerWaiters = new();
    private readonly Dictionary<Guid, Action<IceCandidateDto>> _iceHandlers = new();
    private readonly object _gate = new();

    public event Action<Guid, RemoteDesktopPreferencesDto?> OnStartSessionRequested = delegate { };
    public event Action<Guid> OnStopSessionRequested = delegate { };

    public RemoteDesktopSignalingService(
        IAgentHubInvoker hubInvoker,
        ILogger<RemoteDesktopSignalingService> logger)
    {
        _hubInvoker = hubInvoker;
        _logger = logger;
        _hubInvoker.RegisterHandler<Guid, string, string>("RemoteDesktopAnswer", (sessionId, sdpType, sdp) =>
            CompleteAnswer(sessionId, new RemoteDesktopAnswerDto(sessionId, sdpType, sdp)));
        _hubInvoker.RegisterHandler<Guid, string, string?, int?>("RemoteDesktopIceCandidate", (sessionId, candidate, sdpMid, sdpMLineIndex) =>
            OnRemoteIceCandidate(new IceCandidateDto(sessionId, candidate, sdpMid, sdpMLineIndex)));
        
        // Try to handle both signatures if possible, or assume updated protocol
        _hubInvoker.RegisterHandler<Guid, RemoteDesktopPreferencesDto?>("StartRemoteDesktopSession", (sessionId, prefs) =>
            OnStartSessionRequested(sessionId, prefs));
            
        _hubInvoker.RegisterHandler<Guid>("StopRemoteDesktopSession", (sessionId) =>
            OnStopSessionRequested(sessionId));
    }

    public async Task SendOfferAsync(RemoteDesktopOfferDto offer, CancellationToken cancellationToken = default)
    {
        await _hubInvoker.InvokeAsync("RemoteDesktopOffer", new object?[] { offer.SessionId, offer.DeviceId, offer.SdpType, offer.Sdp }, cancellationToken);
    }

    public async Task SendIceCandidateAsync(IceCandidateDto candidate, CancellationToken cancellationToken = default)
    {
        await _hubInvoker.InvokeAsync("RemoteDesktopIceCandidate", new object?[] { candidate.SessionId, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex }, cancellationToken);
    }

    public Task<RemoteDesktopAnswerDto?> WaitForAnswerAsync(Guid sessionId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<RemoteDesktopAnswerDto?>();
        lock (_gate)
        {
            _answerWaiters[sessionId] = tcs;
        }
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        cts.Token.Register(() =>
        {
            if (tcs.Task.IsCompleted) return;
            lock (_gate) _answerWaiters.Remove(sessionId);
            tcs.TrySetResult(null);
        });
        return tcs.Task;
    }

    public void CompleteAnswer(Guid sessionId, RemoteDesktopAnswerDto answer)
    {
        lock (_gate)
        {
            if (_answerWaiters.Remove(sessionId, out var tcs))
                tcs.TrySetResult(answer);
        }
    }

    public void SubscribeToIceCandidates(Guid sessionId, Action<IceCandidateDto> onCandidate)
    {
        lock (_gate)
            _iceHandlers[sessionId] = onCandidate;
    }

    public void UnsubscribeFromIceCandidates(Guid sessionId)
    {
        lock (_gate)
            _iceHandlers.Remove(sessionId);
    }

    public void OnRemoteIceCandidate(IceCandidateDto candidate)
    {
        lock (_gate)
        {
            if (_iceHandlers.TryGetValue(candidate.SessionId, out var handler))
                handler(candidate);
        }
    }
}
