namespace LabSync.Core.Dto;

public record SetupRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}