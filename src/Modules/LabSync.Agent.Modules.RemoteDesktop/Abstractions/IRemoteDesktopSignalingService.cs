using LabSync.Core.Dto;

namespace LabSync.Agent.Modules.RemoteDesktop.Abstractions;

public interface IRemoteDesktopSignalingService
{
    Task SendOfferAsync(RemoteDesktopOfferDto offer, CancellationToken cancellationToken = default);
    Task SendIceCandidateAsync(IceCandidateDto candidate, CancellationToken cancellationToken = default);
    Task<RemoteDesktopAnswerDto?> WaitForAnswerAsync(Guid sessionId, TimeSpan timeout, CancellationToken cancellationToken = default);
    void SubscribeToIceCandidates(Guid sessionId, Action<IceCandidateDto> onCandidate);
    void UnsubscribeFromIceCandidates(Guid sessionId);
}
