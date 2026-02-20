using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Dto
{
    /// <summary>
    /// Request body for admin login.
    /// </summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }
}
