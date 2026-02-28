using System.Security.Cryptography;
using System.Text;
using LabSync.Core.Interfaces;

namespace LabSync.Server.Services;

public class CryptoService : ICryptoService
{
    public string Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash  = SHA256.HashData(bytes);

        return Convert.ToBase64String(hash);
    }
}