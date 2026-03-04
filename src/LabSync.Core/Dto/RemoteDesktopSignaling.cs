namespace LabSync.Core.Dto;

public record StartRemoteDesktopRequest(
    Guid DeviceId,
    string? RequestedByUserId = null
);

public record RemoteDesktopOfferDto(
    Guid SessionId,
    Guid DeviceId,
    string SdpType,
    string Sdp,
    string[]? AvailableEncoders = null
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

public record RemoteDesktopPreferencesDto(
    int? InitialWidth,
    int? InitialHeight,
    int? InitialFps,
    int? InitialBitrateKbps,
    string? PreferredEncoder
);
