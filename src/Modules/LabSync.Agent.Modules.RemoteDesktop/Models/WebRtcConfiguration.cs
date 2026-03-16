namespace LabSync.Agent.Modules.RemoteDesktop.Models;

public class WebRtcConfiguration
{
    public const string SectionName = "WebRtc";

    public List<IceServerConfig> IceServers { get; set; } = new();
}

public class IceServerConfig
{
    public string? Urls { get; set; }
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
