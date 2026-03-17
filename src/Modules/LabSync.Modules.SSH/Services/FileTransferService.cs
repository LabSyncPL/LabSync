using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LabSync.Modules.SSH.Interfaces;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace LabSync.Modules.SSH.Services;

public class FileTransferService : IFileTransferService
{
    private readonly ILogger<FileTransferService> _logger;

    public FileTransferService(ILogger<FileTransferService> logger)
    {
        _logger = logger;
    }

    public async Task UploadFileAsync(string host, string username, string password, string localFilePath, string remoteFilePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting upload of {LocalPath} to {RemotePath} on {Host}", localFilePath, remoteFilePath, host);

        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException("Local file not found.", localFilePath);
        }

        using var client = new SftpClient(host, username, password);
        
        try
        {
            await client.ConnectAsync(cancellationToken);
            
            using var fileStream = File.OpenRead(localFilePath);           
            await Task.Factory.FromAsync(
                client.BeginUploadFile(fileStream, remoteFilePath),
                client.EndUploadFile);

            _logger.LogInformation("Upload completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to {Host}", host);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    public async Task DownloadFileAsync(string host, string username, string password, string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting download of {RemotePath} from {Host} to {LocalPath}", remoteFilePath, host, localFilePath);

        using var client = new SftpClient(host, username, password);

        try
        {
            await client.ConnectAsync(cancellationToken);

            // Ensure local directory exists
            var directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = File.Create(localFilePath);          
            await Task.Run(() => client.DownloadFile(remoteFilePath, fileStream), cancellationToken);
            _logger.LogInformation("Download completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from {Host}", host);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }
}
