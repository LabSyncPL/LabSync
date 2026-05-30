using LabSync.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Modules.RemoteDesktop.Configuration;

namespace LabSync.Modules.RemoteDesktop.Encoding;

public sealed class LinuxFfmpegVideoEncoder : BaseFfmpegEncoder
{
    private readonly RemoteDesktopConfiguration _config;

    public override bool HandlesCapture => false;

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

        var (outputWidth, outputHeight) = NormalizeOutputResolution(
            options.SourceWidth,
            options.SourceHeight,
            options.OutputWidth,
            options.OutputHeight,
            2);

        string scaleFilter = string.Empty;
        if (outputWidth != options.SourceWidth || outputHeight != options.SourceHeight)
        {
            scaleFilter = $"-vf scale={outputWidth}:{outputHeight}";
        }
        
        string preset = settings.Preset ?? "ultrafast";
        string tune = settings.Tune ?? "zerolatency";
        string profile = settings.Profile ?? "baseline";

        int gopSize = fps;
        string encoderArgs = $"-c:v libx264 -pix_fmt yuv420p -profile:v {profile} -preset {preset} -tune {tune} " +
                             $"-b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k " +
                             $"-g {gopSize} -keyint_min {gopSize} -sc_threshold 0 -bf 0 -slices 1 -threads 0";
   
        return $"-hide_banner -loglevel warning -f rawvideo -pix_fmt bgra -s {options.SourceWidth}x{options.SourceHeight} -r {fps} -i - {scaleFilter} {encoderArgs} -f h264 -an -";
    }
}
