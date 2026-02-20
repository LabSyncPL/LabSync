namespace LabSync.Core.Dto;

/// <summary>
/// Response for GET /api/system/status. Tells the client whether setup has been completed.
/// </summary>
public class SystemStatusResponse
{
    /// <summary>
    /// True if at least one administrator has been created (setup complete). False = show Setup Wizard.
    /// </summary>
    public bool SetupComplete { get; set; }
}
