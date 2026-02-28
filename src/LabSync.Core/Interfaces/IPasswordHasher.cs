namespace LabSync.Core.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}