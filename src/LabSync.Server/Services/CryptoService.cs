using System.Security.Cryptography;
using System.Text;
using LabSync.Core.Interfaces;

namespace LabSync.Server.Services;

/// <summary>
/// A simple implementation of ICryptoService that uses SHA256 for hashing.
/// </summary>
public class CryptoService : ICryptoService
{
    /// <summary>
    /// Hashes a string using SHA256.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>The SHA256 hash of the string.</returns>
    public string Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
