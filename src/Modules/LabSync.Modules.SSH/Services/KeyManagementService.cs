using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using LabSync.Modules.SSH.Interfaces;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace LabSync.Modules.SSH.Services;

public class KeyManagementService : IKeyManagementService
{
    private readonly ILogger<KeyManagementService> _logger;

    public KeyManagementService(ILogger<KeyManagementService> logger)
    {
        _logger = logger;
    }

    public async Task<string> EnsureKeyAsync(string? keyPath = null, string? passphrase = null)
    {
        if (string.IsNullOrEmpty(keyPath))
        {
            // Default to a secure location in the user's profile
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var keyDir = Path.Combine(appData, "LabSync", "SSH");
            if (!Directory.Exists(keyDir))
            {
                Directory.CreateDirectory(keyDir);
                SecureDirectory(keyDir);
            }
            keyPath = Path.Combine(keyDir, "id_rsa_labsync");
        }

        if (File.Exists(keyPath))
        {
            _logger.LogInformation("Using existing SSH key at {KeyPath}", keyPath);
            return keyPath;
        }

        _logger.LogInformation("Generating new SSH key pair at {KeyPath}", keyPath);

        using (var rsa = RSA.Create(4096))
        {
            var privateKeyBytes = rsa.ExportRSAPrivateKey();
            var privateKeyPem = "-----BEGIN RSA PRIVATE KEY-----\n" + 
                                Convert.ToBase64String(privateKeyBytes, Base64FormattingOptions.InsertLineBreaks) + 
                                "\n-----END RSA PRIVATE KEY-----";

            if (!string.IsNullOrEmpty(passphrase))
            {
                 _logger.LogWarning("Passphrase protection for generated keys is not currently supported with built-in generator. Key will be unencrypted.");
            }

            var publicKeyOpenSsh = ExportPublicKeyToOpenSsh(rsa);

            await File.WriteAllTextAsync(keyPath, privateKeyPem);
            await File.WriteAllTextAsync(keyPath + ".pub", publicKeyOpenSsh);
            
            SecureFile(keyPath);
        }

        _logger.LogInformation("SSH key pair generated successfully.");
        return keyPath;
    }

    private string ExportPublicKeyToOpenSsh(RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);
        var exponent = parameters.Exponent;
        var modulus = parameters.Modulus;
        
        if (exponent == null || modulus == null) throw new InvalidOperationException("Invalid RSA parameters");

        using (var stream = new MemoryStream())
        {
        
            WriteSshString(stream, Encoding.ASCII.GetBytes("ssh-rsa"));
            WriteSshString(stream, exponent);
            
            if ((modulus[0] & 0x80) != 0)
            {
                var newModulus = new byte[modulus.Length + 1];
                Buffer.BlockCopy(modulus, 0, newModulus, 1, modulus.Length);
                WriteSshString(stream, newModulus);
            }
            else
            {
                WriteSshString(stream, modulus);
            }

            var base64 = Convert.ToBase64String(stream.ToArray());
            return $"ssh-rsa {base64} labsync-agent";
        }
    }

    private void WriteSshString(Stream stream, byte[] data)
    {
        var length = data.Length;
        var lengthBytes = BitConverter.GetBytes(length);
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
        
        stream.Write(lengthBytes, 0, 4);
        stream.Write(data, 0, data.Length);
    }

    public string GetPublicKey(string keyPath)
    {
        var pubKeyPath = keyPath + ".pub";
        if (File.Exists(pubKeyPath))
        {
            return File.ReadAllText(pubKeyPath).Trim();
        }
        
        throw new FileNotFoundException("Public key file not found.", pubKeyPath);
    }

    public PrivateKeyFile GetPrivateKeyFile(string keyPath, string? passphrase = null)
    {
        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException("Private key file not found.", keyPath);
        }

        return new PrivateKeyFile(keyPath, passphrase);
    }

    private string GetKnownHostsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "LabSync", "SSH");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, "known_hosts");
    }

    public bool IsHostKeyTrusted(string host, string algorithm, byte[] hostKey)
    {
        var path = GetKnownHostsPath();
        if (!File.Exists(path)) return false;

        var keyBase64 = Convert.ToBase64String(hostKey);
        
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var hosts = parts[0].Split(',');
                foreach (var h in hosts)
                {
                    if (h == host)
                    {
                        if (parts[1] == algorithm && parts[2] == keyBase64)
                        {
                            return true;
                        }
                        return false;
                    }
                }
            }
        }
        return false;
    }

    public void ValidateOrAddHostKey(string host, string algorithm, byte[] hostKey)
    {
        var path = GetKnownHostsPath();
        var keyBase64 = Convert.ToBase64String(hostKey);
        
        if (File.Exists(path))
        {
            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var hosts = parts[0].Split(',');
                    if (hosts.Contains(host))
                    {
                        // Host found
                        if (parts[1] == algorithm && parts[2] == keyBase64)
                        {
                            // Match
                            return;
                        }
                        else
                        {
                            // Mismatch!
                            _logger.LogError("Host key verification failed for {Host}. Expected {ExpectedAlgorithm} {ExpectedKey}, got {Algorithm} {Key}", 
                                host, parts[1], parts[2], algorithm, keyBase64);
                            throw new SshConnectionException($"Host key verification failed for {host}. Possible man-in-the-middle attack.");
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Trusting new host {Host} (TOFU).", host);
        var entry = $"{host} {algorithm} {keyBase64}";
        File.AppendAllLines(path, new[] { entry });
        SecureFile(path);
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
