namespace LabSync.Core.Interfaces;

/// <summary>
/// Hashes and verifies passwords (e.g. for admin users). Uses a salt and slow hash suitable for passwords.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password. Result can be stored and later verified with <see cref="Verify"/>.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a password against a stored hash. Returns true if the password matches.
    /// </summary>
    bool Verify(string password, string storedHash);
}
