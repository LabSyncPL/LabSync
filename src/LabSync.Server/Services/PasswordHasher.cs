using LabSync.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace LabSync.Server.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int  SaltSize   = 16;
    private const int  HashSize   = 32;
    private const int  Iterations = 100_000;
    private const char Separator  = ':';

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Convert.ToBase64String(salt)}{Separator}{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split(Separator);
        if (parts.Length != 2)
            return false;

        if (!Convert.TryFromBase64String(parts[0], new Span<byte>(new byte[SaltSize]), out _))
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
