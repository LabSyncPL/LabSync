namespace LabSync.Core.ValueObjects
{
    /// <summary>
    /// Represents the business lifecycle state of the device within the RMM system.
    /// </summary>
    public enum DeviceStatus
    {
        Pending = 0,
        Offline = 1,      
        Online = 2,  
        Maintenance = 3,
        Blocked = 4
    }
}