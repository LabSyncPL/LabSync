using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Agent.Modules.RemoteDesktop.Configuration;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

/// <summary>
/// Linux-only H.264 encoder using ffmpeg with x11grab.
/// Acts as both screen capturer and encoder.
/// </summary>
public sealed class LinuxFfmpegVideoEncoder : BaseFfmpegEncoder
{
    private readonly RemoteDesktopConfiguration _config;

    public override bool HandlesCapture => true;

    public LinuxFfmpegVideoEncoder(
        ILogger<LinuxFfmpegVideoEncoder> logger,
        IOptions<RemoteDesktopConfiguration> options,
        int channelCapacity)
        : base(logger, channelCapacity, options.Value.Encoding.FfmpegPath)
    {
        _config = options.Value;
    }

    protected override string BuildFfmpegArguments(EncoderOptions options)
    {
        var settings = _config.Encoding.LinuxSoftware;

        int bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : _config.Encoding.DefaultBitrateKbps;
        if (bitrate <= 0) bitrate = 1500;
        
        int fps = options.TargetFps > 0 ? options.TargetFps : _config.Encoding.DefaultFps;
        if (fps <= 0) fps = 30;
        
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        if (string.IsNullOrWhiteSpace(display))
        {
            display = ":0.0";
        }
        
        var args = $"-f x11grab -draw_mouse 1 -framerate {fps} -s {options.SourceWidth}x{options.SourceHeight} -i {display} ";

        string scaleFilter = "";
        if (options.OutputWidth != options.SourceWidth || options.OutputHeight != options.SourceHeight)
        {
            scaleFilter = $"-vf scale={options.OutputWidth}:{options.OutputHeight}";
        }
        
        string preset = settings.Preset ?? "ultrafast";
        string tune = settings.Tune ?? "zerolatency";
        string profile = settings.Profile ?? "baseline";

        int gopSize = fps;   
        string encoderArgs = $"-c:v libx264 -pix_fmt yuv420p -profile:v {profile} -preset {preset} -tune {tune} " +
                             $"-b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k " +
                             $"-g {gopSize} -keyint_min {gopSize} -sc_threshold 0 -bf 0 -slices 1 -threads 0";
   
        return $"{args} {scaleFilter} {encoderArgs} -f h264 -an -";
    }
}
