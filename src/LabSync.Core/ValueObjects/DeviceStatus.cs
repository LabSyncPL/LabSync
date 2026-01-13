namespace LabSync.Core.ValueObjects
{
    /// <summary>
    /// Represents the business lifecycle state of the device within the RMM system.
    /// </summary>
    public enum DeviceStatus
    {
        /// <summary>
        /// Device registered but waiting for administrator approval.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Device is authorized and fully operational.
        /// </summary>
        Active = 1,

        /// <summary>
        /// Device is explicitly blocked by administrator (e.g., stolen or decommissioned).
        /// Agent requests will be rejected.
        /// </summary>
        Blocked = 2,

        /// <summary>
        /// Device is in maintenance mode (no alerts will be triggered).
        /// </summary>
        Maintenance = 3
    }
}