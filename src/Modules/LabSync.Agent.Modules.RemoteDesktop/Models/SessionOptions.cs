namespace LabSync.Agent.Modules.RemoteDesktop.Models;

public sealed class SessionOptions
{
    public static readonly SessionOptions Default = new();

    public TimeSpan OfferTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan IceGatheringTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan ConnectionStateTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan StopGracePeriod { get; init; } = TimeSpan.FromMilliseconds(500);

    public int CaptureChannelCapacity { get; init; } = 3;
    public int EncodedChannelCapacity { get; init; } = 2;
}
