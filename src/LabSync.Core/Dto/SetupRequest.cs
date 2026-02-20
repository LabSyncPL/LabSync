using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Dto;

/// <summary>
/// Request body for initial system setup (create first administrator).
/// </summary>
public class SetupRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [MinLength(2, ErrorMessage = "Username must be at least 2 characters.")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;
}
