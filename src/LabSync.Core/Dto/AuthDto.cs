namespace LabSync.Core.Dto;

public record LoginRequest(
    string Username,
    string Password
);

public record LoginResponse(
    string AccessToken,
    int ExpiresInSeconds,
    string TokenType = "Bearer"
);