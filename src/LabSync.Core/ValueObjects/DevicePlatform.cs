namespace LabSync.Core.ValueObjects
{
    /// <summary>
    /// Represents the operating system family of the device.
    /// Used to determine which script syntax (Bash vs PowerShell) to execute.
    /// </summary>
    public enum DevicePlatform
    {
        Unknown = 0,
        Windows = 1,
        Linux = 2,
        MacOS = 3 
    }
}
