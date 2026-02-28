namespace LabSync.Core.Dto;

public record StartRemoteDesktopRequest(
    Guid DeviceId,
    string? RequestedByUserId = null
);

public record RemoteDesktopOfferDto(
    Guid SessionId,
    Guid DeviceId,
    string SdpType,
    string Sdp
);

public record RemoteDesktopAnswerDto(
    Guid SessionId,
    string SdpType,
    string Sdp
);

public record IceCandidateDto(
    Guid SessionId,
    string Candidate,
    string? SdpMid,
    int? SdpMLineIndex
);
