using LabSync.Modules.RemoteDesktop.Models;

namespace LabSync.Modules.RemoteDesktop.Configuration;

public class RemoteDesktopConfiguration
{
    public const string SectionName = "RemoteDesktop";

    public SessionConfiguration Session { get; set; } = new();
    public VideoEncodingConfiguration Encoding { get; set; } = new();
    public ScreenCaptureConfiguration Capture { get; set; } = new();
}

public class SessionConfiguration 
{
    public TimeSpan OfferTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan IceGatheringTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan ConnectionStateTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan StopGracePeriod { get; set; } = TimeSpan.FromMilliseconds(500);
    public int InputBufferSize { get; set; } = 4096;
}

public class VideoEncodingConfiguration
{
    public int DefaultBitrateKbps { get; set; } = 2000;
    public int DefaultFps { get; set; } = 30;
    public int EncodedChannelCapacity { get; set; } = 2;
    public string FfmpegPath { get; set; } = "ffmpeg";
    
    public FfmpegEncoderSettings WindowsNvidia { get; set; } = new() { Preset = "p1", Rc = "cbr" };
    public FfmpegEncoderSettings WindowsAmd { get; set; } = new() { Usage = "ultra_low_latency", Rc = "cbr" };
    public FfmpegEncoderSettings WindowsIntel { get; set; } = new() { Preset = "veryfast", AsyncDepth = 1 };
    public FfmpegEncoderSettings Software { get; set; } = new() { Preset = "fast", Tune = "zerolatency", Profile = "baseline" };
    public FfmpegEncoderSettings LinuxSoftware { get; set; } = new() { Preset = "ultrafast", Tune = "zerolatency", Profile = "baseline" };
}

public class FfmpegEncoderSettings
{
    public string? Preset { get; set; }
    public string? Tune { get; set; }
    public string? Profile { get; set; }
    public string? Rc { get; set; }
    public string? Usage { get; set; }
    public int? AsyncDepth { get; set; }
}

public class ScreenCaptureConfiguration
{
    public int ChannelCapacity { get; set; } = 3;
    public int TargetFps { get; set; } = 20;
    public int PlaceholderDelayMs { get; set; } = 33;
}
