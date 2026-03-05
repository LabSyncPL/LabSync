using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Modules.RemoteDesktop.Encoding;

/// <summary>
/// Windows-only H.264 encoder using an external ffmpeg process.
/// Expects raw BGRA frames and produces Annex B H.264 NAL units.
/// </summary>
public sealed class WindowsFfmpegVideoEncoder : BaseFfmpegEncoder
{
    public override bool HandlesCapture => false;

    public WindowsFfmpegVideoEncoder(ILogger logger, int channelCapacity, string ffmpegPath = "ffmpeg")
        : base(logger, channelCapacity, ffmpegPath)
    {
    }

    protected override string BuildFfmpegArguments(EncoderOptions options)
    {
        var bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : 1500;
        var fps = options.TargetFps > 0 ? options.TargetFps : 30;
        
        // Input options: raw frames from stdin
        var args = $"-f rawvideo -pix_fmt bgra -s {options.SourceWidth}x{options.SourceHeight} -r {fps} -i - ";

        // Scaling logic
        string scaleFilter = "";
        if (options.OutputWidth != options.SourceWidth || options.OutputHeight != options.SourceHeight)
        {
            // Use -2 to keep aspect ratio if one dimension is provided, or explicit size
            scaleFilter = $"-vf scale={options.OutputWidth}:{options.OutputHeight}";
        }

        // Encoder selection
        string encoderArgs = options.EncoderType switch
        {
            VideoEncoderType.NvidiaNvenc => $"-c:v h264_nvenc -preset p1 -rc:v cbr -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps} -zerolatency 1",
            VideoEncoderType.AmdAmf => $"-c:v h264_amf -usage ultra_low_latency -rc cbr -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps}",
            VideoEncoderType.IntelQsv => $"-c:v h264_qsv -preset veryfast -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps} -async_depth 1",
            _ => $"-c:v libx264 -pix_fmt yuv420p -profile:v baseline -preset fast -tune zerolatency -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bitrate * 2}k -g {fps} -keyint_min {fps} -sc_threshold 0 -bf 0 -slices 1 -threads 0"
        };

        // Combine
        return $"{args} {scaleFilter} {encoderArgs} -f h264 -an -";
    }
}
