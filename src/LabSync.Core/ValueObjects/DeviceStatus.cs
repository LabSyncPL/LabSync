namespace LabSync.Core.ValueObjects
{
    /// <summary>
    /// Represents the business lifecycle state of the device within the RMM system.
    /// </summary>
    public enum DeviceStatus
    {
        Pending     = 0,
        Active      = 1,       
        Maintenance = 2,  
        Blocked     = 3
    }
}