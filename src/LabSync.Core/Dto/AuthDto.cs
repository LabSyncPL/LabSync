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

public record RegisterRequest(
    string Username,
    string Password
);

public record AccountProfileDto(
    string Username,
    DateTime CreatedAt
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ChangeUsernameRequest(
    string NewUsername,
    string CurrentPassword
);