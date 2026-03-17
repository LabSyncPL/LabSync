using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LabSync.Modules.SSH.Interfaces;

/// <summary>
/// Provides file transfer capabilities using SFTP and SCP protocols.
/// Wraps SSH.NET's SftpClient and ScpClient functionality.
/// </summary>
public interface IFileTransferService
{
    /// <summary>
    /// Uploads a file to the remote server asynchronously via SFTP.
    /// </summary>
    /// <param name="host">The remote host address.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication (or key path if supported).</param>
    /// <param name="localFilePath">The full path to the local file to upload.</param>
    /// <param name="remoteFilePath">The destination path on the remote server.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UploadFileAsync(string host, string username, string password, string localFilePath, string remoteFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from the remote server asynchronously via SFTP.
    /// </summary>
    /// <param name="host">The remote host address.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="remoteFilePath">The path to the file on the remote server.</param>
    /// <param name="localFilePath">The destination path on the local machine.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DownloadFileAsync(string host, string username, string password, string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default);
}
