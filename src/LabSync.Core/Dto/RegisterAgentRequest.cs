using LabSync.Core.Types;

namespace LabSync.Core.Dto;

public record RegisterAgentRequest(
    string MacAddress,
    string Hostname,
    DevicePlatform Platform,
    string OsVersion,
    string? IpAddress
);

public record RegisterAgentResponse(
    Guid DeviceId,
    string? Token,
    string? Message
);