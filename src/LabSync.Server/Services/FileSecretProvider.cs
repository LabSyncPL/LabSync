using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using LabSync.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabSync.Server.Services;

public class FileSecretProvider : ISecretProvider
{
    private readonly ILogger<FileSecretProvider> _logger;
    private readonly string _storagePath;
    private readonly ICryptoService _cryptoService; 

    private readonly byte[] _encryptionKey; 

    public FileSecretProvider(ILogger<FileSecretProvider> logger, ICryptoService cryptoService, string? storagePath = null)
    {
        _logger = logger;
        _cryptoService = cryptoService;
        
        if (string.IsNullOrEmpty(storagePath))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _storagePath = Path.Combine(appData, "LabSync", "SSH", "keys");
        }
        else
        {
            _storagePath = storagePath;
        }

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
            SecureDirectory(_storagePath);
        }

        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName + "LabSyncSecretKey"));
    }

    public async Task<string> StoreSecretAsync(string secretContent, string? metadata = null)
    {
        var secretId = Guid.NewGuid().ToString();
        var filePath = Path.Combine(_storagePath, secretId + ".enc");

        try
        {
            var encryptedData = Encrypt(secretContent);
            await File.WriteAllBytesAsync(filePath, encryptedData);
            SecureFile(filePath);
            
            _logger.LogInformation("Stored secret {SecretId} securely.", secretId);
            return secretId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store secret.");
            throw;
        }
    }

    public async Task<string> RetrieveSecretAsync(string secretReference)
    {
        var filePath = Path.Combine(_storagePath, secretReference + ".enc");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Secret not found.", secretReference);
        }

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(filePath);
            return Decrypt(encryptedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretReference}.", secretReference);
            throw;
        }
    }

    public Task DeleteSecretAsync(string secretReference)
    {
        var filePath = Path.Combine(_storagePath, secretReference + ".enc");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted secret {SecretReference}.", secretReference);
        }
        return Task.CompletedTask;
    }

    private byte[] Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        
        ms.Write(aes.IV, 0, aes.IV.Length);
        
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }
        
        return ms.ToArray();
    }

    private string Decrypt(byte[] cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;

        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(cipherText, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipherText, iv.Length, cipherText.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        
        return sr.ReadToEnd();
    }

    private void SecureDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var directoryInfo = new DirectoryInfo(path);
                var security = directoryInfo.GetAccessControl();
                security.SetAccessRuleProtection(true, false);
                var currentUser = WindowsIdentity.GetCurrent();
                security.AddAccessRule(new FileSystemAccessRule(currentUser.User, FileSystemRights.FullControl, AccessControlType.Allow));
                directoryInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set secure permissions on directory {Path}", path);
            }
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set secure permissions on directory {Path}", path);
            }
        }
    }

    private void SecureFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var fileInfo = new FileInfo(path);
                var security = fileInfo.GetAccessControl();
                security.SetAccessRuleProtection(true, false);
                var currentUser = WindowsIdentity.GetCurrent();
                security.AddAccessRule(new FileSystemAccessRule(currentUser.User, FileSystemRights.FullControl, AccessControlType.Allow));
                fileInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set secure permissions on file {Path}", path);
            }
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set secure permissions on file {Path}", path);
            }
        }
    }
}
