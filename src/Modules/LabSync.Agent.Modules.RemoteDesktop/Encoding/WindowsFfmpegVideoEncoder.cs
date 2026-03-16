using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Agent.Modules.RemoteDesktop.Configuration;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

public class WindowsFfmpegVideoEncoder : BaseFfmpegEncoder
{
    private readonly RemoteDesktopConfiguration _config;

    public override bool HandlesCapture => false;

    public WindowsFfmpegVideoEncoder(
        ILogger<WindowsFfmpegVideoEncoder> logger,
        IOptions<RemoteDesktopConfiguration> options,
        int channelCapacity) 
        : base(logger, channelCapacity, options.Value.Encoding.FfmpegPath)
    {
        _config = options.Value;
    }

    protected override string BuildFfmpegArguments(EncoderOptions options)
    {
        var settings = _config.Encoding.WindowsNvidia; // Default to Nvidia for now
        
        string preset = settings.Preset ?? "p1";
        string rc = settings.Rc ?? "cbr";
        int bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : _config.Encoding.DefaultBitrateKbps;
        if (bitrate <= 0) bitrate = 2000;

        return $"-f rawvideo -pix_fmt bgra -s {options.SourceWidth}x{options.SourceHeight} -r {options.TargetFps} -i - " +
               $"-c:v h264_nvenc -preset {preset} -rc {rc} -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k " +
               $"-zerolatency 1 -f h264 -";
    }
}
