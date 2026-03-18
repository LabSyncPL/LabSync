using System.Threading.Tasks;

namespace LabSync.Core.Interfaces;

/// <summary>
/// Defines a contract for secure storage and retrieval of sensitive secrets (e.g. SSH keys).
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Stores a secret securely and returns a reference identifier.
    /// </summary>
    /// <param name="secretContent">The plaintext content of the secret.</param>
    /// <param name="metadata">Optional metadata/context for the secret.</param>
    /// <returns>A unique reference string (e.g. file path or vault ID).</returns>
    Task<string> StoreSecretAsync(string secretContent, string? metadata = null);

    /// <summary>
    /// Retrieves the plaintext secret using its reference identifier.
    /// </summary>
    /// <param name="secretReference">The reference identifier returned by StoreSecretAsync.</param>
    /// <returns>The plaintext secret.</returns>
    Task<string> RetrieveSecretAsync(string secretReference);

    /// <summary>
    /// Deletes a stored secret.
    /// </summary>
    /// <param name="secretReference">The reference identifier.</param>
    Task DeleteSecretAsync(string secretReference);
}
