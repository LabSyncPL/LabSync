using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace LabSync.Modules.SSH.Interfaces;

public interface IKeyManagementService
{
    /// <summary>
    /// Ensures a valid SSH key pair exists at the specified path or default location.
    /// If no key exists, generates a new one.
    /// </summary>
    /// <param name="keyPath">Optional path to the private key file. If null, uses default.</param>
    /// <param name="passphrase">Optional passphrase for the key.</param>
    /// <returns>The path to the private key.</returns>
    Task<string> EnsureKeyAsync(string? keyPath = null, string? passphrase = null);

    /// <summary>
    /// Gets the public key in OpenSSH format.
    /// </summary>
    /// <param name="keyPath">Path to the private key.</param>
    /// <returns>The public key string.</returns>
    string GetPublicKey(string keyPath);

    /// <summary>
    /// Gets a PrivateKeyFile object for use with SshClient/SftpClient.
    /// </summary>
    /// <param name="keyPath">Path to the private key.</param>
    /// <param name="passphrase">Optional passphrase.</param>
    /// <returns>PrivateKeyFile instance.</returns>
    PrivateKeyFile GetPrivateKeyFile(string keyPath, string? passphrase = null);

    /// <summary>
    /// Validates the host key against known_hosts.
    /// </summary>
    /// <param name="host">The host being connected to.</param>
    /// <param name="algorithm">The host key algorithm (e.g. ssh-rsa).</param>
    /// <param name="hostKey">The host key data.</param>
    /// <returns>True if trusted, false otherwise.</returns>
    bool IsHostKeyTrusted(string host, string algorithm, byte[] hostKey);

    /// <summary>
    /// Validates the host key. If unknown, adds it (TOFU). If mismatch, throws exception.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="hostKey">The host key.</param>
    void ValidateOrAddHostKey(string host, string algorithm, byte[] hostKey);
}
