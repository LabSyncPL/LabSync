namespace LabSync.Core.Entities;

public class AdminUser
{
    public Guid Id { get; init; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime CreatedAt { get; init; }

    protected AdminUser() { }

    public AdminUser(string username, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));

        Id = Guid.NewGuid();
        Username     = username;
        PasswordHash = passwordHash;
        CreatedAt    = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
    }

    public void ChangeUsername(string newUsername)
    {
        if (string.IsNullOrWhiteSpace(newUsername))
            throw new ArgumentException("Username cannot be empty.", nameof(newUsername));

        Username = newUsername;
    }
}