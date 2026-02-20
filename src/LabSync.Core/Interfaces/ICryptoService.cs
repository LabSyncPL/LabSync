namespace LabSync.Core.Interfaces;

/// <summary>
/// Defines an interface for cryptographic operations, such as hashing.
/// </summary>
public interface ICryptoService
{
    /// <summary>
    /// Hashes a given string.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>The hashed string.</returns>
    string Hash(string input);
}
