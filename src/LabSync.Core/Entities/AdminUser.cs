using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Entities;

/// <summary>
/// Administrator account for the web panel. Created during initial setup; used for login.
/// </summary>
public class AdminUser
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
