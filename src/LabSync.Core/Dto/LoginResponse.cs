namespace LabSync.Core.Dto
{
    /// <summary>
    /// Response returned after successful admin login.
    /// </summary>
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresInSeconds { get; set; }
    }
}
