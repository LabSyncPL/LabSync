using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

/// <summary>
/// Linux-only H.264 encoder using ffmpeg with x11grab.
/// Acts as both screen capturer and encoder.
/// </summary>
public sealed class LinuxFfmpegVideoEncoder : BaseFfmpegEncoder
{
    public override bool HandlesCapture => true;

    public LinuxFfmpegVideoEncoder(ILogger logger, int channelCapacity, string ffmpegPath = "ffmpeg")
        : base(logger, channelCapacity, ffmpegPath)
    {
    }

    protected override string BuildFfmpegArguments(EncoderOptions options)
    {
        var bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : 1500;
        var fps = options.TargetFps > 0 ? options.TargetFps : 30;
        
        // Detect display environment variable or default to :0.0
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        if (string.IsNullOrWhiteSpace(display))
        {
            display = ":0.0";
        }
        
        Logger.LogInformation("Using DISPLAY: {Display}", display);

        // Input options: x11grab
        // -draw_mouse 1: include mouse cursor
        // -s: capture size (must match actual screen size for x11grab usually, unless we want cropping/scaling at capture time)
        // -framerate: capture framerate (preferred over -r for input device)
        // -i: input device (display)
        var args = $"-f x11grab -draw_mouse 1 -framerate {fps} -s {options.SourceWidth}x{options.SourceHeight} -i {display} ";

        string scaleFilter = "";
        if (options.OutputWidth != options.SourceWidth || options.OutputHeight != options.SourceHeight)
        {
            scaleFilter = $"-vf scale={options.OutputWidth}:{options.OutputHeight}";
        }

        int gopSize = fps;   
        string encoderArgs = $"-c:v libx264 -pix_fmt yuv420p -profile:v baseline -preset ultrafast -tune zerolatency " +
                             $"-b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k " +
                             $"-g {gopSize} -keyint_min {gopSize} -sc_threshold 0 -bf 0 -slices 1 -threads 0";

        return $"{args} {scaleFilter} {encoderArgs} -f h264 -an -";
    }
}
