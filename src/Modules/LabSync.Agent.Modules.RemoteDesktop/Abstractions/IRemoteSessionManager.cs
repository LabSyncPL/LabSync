using LabSync.Core.Dto;

namespace LabSync.Agent.Modules.RemoteDesktop.Abstractions;

public interface IRemoteSessionManager
{
    Task<RemoteSessionResult> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default);
    Task StopSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    bool IsSessionActive(Guid sessionId);
}

public record StartSessionRequest(
    Guid DeviceId,
    string? RequestedByUserId,
    Guid? SessionId = null,
    RemoteDesktopPreferencesDto? Preferences = null
);

public record RemoteSessionResult(
    bool Success,
    Guid? SessionId,
    string? ErrorMessage
);
