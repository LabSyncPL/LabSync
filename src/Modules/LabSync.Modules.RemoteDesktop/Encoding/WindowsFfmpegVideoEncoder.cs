using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Modules.RemoteDesktop.Configuration;
using LabSync.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Modules.RemoteDesktop.Encoding;

/// <summary>
/// FFmpeg video encoder for Windows with support for multiple hardware acceleration backends.
/// Supports NVIDIA NVENC, Intel Quick Sync Video (QSV), AMD AMF, and software encoding.
/// </summary>
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

    /// <summary>
    /// Builds FFmpeg command-line arguments for video encoding.
    /// Automatically selects the appropriate codec and preset based on EncoderOptions.EncoderType.
    /// Handles dimension normalization and intelligent filtering.
    /// </summary>
    protected override string BuildFfmpegArguments(EncoderOptions options)
    {
        // Validate and determine bitrate
        int bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : _config.Encoding.DefaultBitrateKbps;
        if (bitrate <= 0) bitrate = 2000;

        // Normalize output resolution to nearest aligned dimensions while preserving aspect ratio
        const int alignment = 8;
        var (outputWidth, outputHeight) = NormalizeOutputResolution(
            options.SourceWidth,
            options.SourceHeight,
            options.OutputWidth,
            options.OutputHeight,
            alignment);

        Logger.LogDebug(
            "FFmpeg encoder normalized output resolution from {RequestedWidth}x{RequestedHeight} to {OutputWidth}x{OutputHeight}",
            options.OutputWidth, options.OutputHeight, outputWidth, outputHeight);

        // Calculate frame size for blocksize parameter (BGRA = 4 bytes per pixel)
        long frameSize = (long)options.SourceWidth * options.SourceHeight * 4;

        // Build video filter chain for format conversion and scaling
        string videoFilter = BuildVideoFilterChain(options.SourceWidth, options.SourceHeight, outputWidth, outputHeight);

        // Select encoder settings based on requested encoder type
        var (codecName, encoderArgs) = GetEncoderSettings(options.EncoderType, bitrate, options.TargetFps);

        // Construct final FFmpeg command
        return $"-f rawvideo -pix_fmt bgra -s {options.SourceWidth}x{options.SourceHeight} " +
               $"-blocksize {frameSize} -r {options.TargetFps} -i - " +
               $"{videoFilter} " +
               $"{encoderArgs} " +
               $"-f h264 -";
    }

    /// <summary>
    /// Builds the video filter chain for format conversion and resizing.
    /// Ensures output is in yuv420p format (required by most H.264 decoders).
    /// </summary>
    private string BuildVideoFilterChain(int sourceWidth, int sourceHeight, int outputWidth, int outputHeight)
    {
        var filterParts = new List<string>();

        // Add scaling if output dimensions differ from input
        if (outputWidth != sourceWidth || outputHeight != sourceHeight)
        {
            filterParts.Add($"scale={outputWidth}:{outputHeight}");
        }

        // Force YUV420P format for H.264 compatibility
        filterParts.Add("format=yuv420p");

        if (filterParts.Count == 0)
        {
            return string.Empty;
        }

        string filterChain = string.Join(",", filterParts);
        return $"-vf \"{filterChain}\"";
    }

    /// <summary>
    /// Selects the appropriate video codec and encoder settings based on requested encoder type.
    /// Returns codec name and FFmpeg encoder-specific arguments.
    /// </summary>
    private (string CodecName, string EncoderArgs) GetEncoderSettings(VideoEncoderType encoderType, int bitrate, int fps)
    {
        return encoderType switch
        {
            VideoEncoderType.NvidiaNvenc => GetNvidiaSettings(bitrate, fps),
            VideoEncoderType.IntelQsv => GetIntelSettings(bitrate, fps),
            VideoEncoderType.AmdAmf => GetAmdSettings(bitrate, fps),
            VideoEncoderType.Software => GetSoftwareSettings(bitrate, fps),
            _ => GetSoftwareSettings(bitrate, fps)
        };
    }

    /// <summary>
    /// NVIDIA NVENC settings - Hardware acceleration via NVIDIA GPUs (GeForce, Quadro, RTX, Tesla).
    /// </summary>
    private (string CodecName, string EncoderArgs) GetNvidiaSettings(int bitrate, int fps)
    {
        var settings = _config.Encoding.WindowsNvidia;
        
        string preset = settings.Preset ?? "hp"; // hp, hq, lossless, default
        string rc = settings.Rc ?? "cbr"; // cbr, vbr, cbr_ld_hq, cbr_hq, vbr_hq

        int bufsize = Math.Max(bitrate, bitrate * 2);
        int gop = fps;

        string encoderArgs = $"-c:v h264_nvenc " +
                             $"-preset {preset} " +
                             $"-rc {rc} " +
                             $"-b:v {bitrate}k " +
                             $"-maxrate {bitrate}k " +
                             $"-bufsize {bufsize}k " +
                             $"-g {gop} " +
                             $"-bf 0 " +
                             $"-zerolatency 1";

        return ("h264_nvenc", encoderArgs);
    }

    /// <summary>
    /// Intel Quick Sync Video (QSV) settings - Hardware acceleration via Intel iGPU/dGPU (Arc, UHD, HD Graphics).
    /// </summary>
    private (string CodecName, string EncoderArgs) GetIntelSettings(int bitrate, int fps)
    {
        var settings = _config.Encoding.WindowsIntel;
        
        string preset = settings.Preset ?? "veryfast"; // veryslow, slower, slow, medium, fast, veryfast
        int asyncDepth = settings.AsyncDepth > 0 ? settings.AsyncDepth : 1; // Keep low for latency

        int bufsize = Math.Max(bitrate, bitrate * 2);
        int gop = fps;

        string encoderArgs = $"-c:v h264_qsv " +
                             $"-preset {preset} " +
                             $"-b:v {bitrate}k " +
                             $"-maxrate {bitrate}k " +
                             $"-bufsize {bufsize}k " +
                             $"-g {gop} " +
                             $"-async_depth {asyncDepth}";

        return ("h264_qsv", encoderArgs);
    }

    /// <summary>
    /// AMD Advanced Media Framework (AMF) settings - Hardware acceleration via AMD Radeon GPUs.
    /// </summary>
    private (string CodecName, string EncoderArgs) GetAmdSettings(int bitrate, int fps)
    {
        var settings = _config.Encoding.WindowsAmd;
        
        string usage = settings.Usage ?? "ultra_low_latency"; // transcoding, ultra_low_latency, lowlatency, webcam
        string rc = settings.Rc ?? "cbr"; // cbr, vbr, hqvbr

        int bufsize = Math.Max(bitrate, bitrate * 2);
        int gop = fps;

        string encoderArgs = $"-c:v h264_amf " +
                             $"-usage {usage} " +
                             $"-rc {rc} " +
                             $"-b:v {bitrate}k " +
                             $"-maxrate {bitrate}k " +
                             $"-bufsize {bufsize}k " +
                             $"-g {gop}";

        return ("h264_amf", encoderArgs);
    }

    /// <summary>
    /// Software encoding via libx264 - Fallback for systems without hardware acceleration.
    /// Trades CPU usage for maximum compatibility.
    /// </summary>
    private (string CodecName, string EncoderArgs) GetSoftwareSettings(int bitrate, int fps)
    {
        var settings = _config.Encoding.Software;
        
        string preset = settings.Preset ?? "ultrafast"; // ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow
        string tune = settings.Tune ?? "zerolatency"; // film, animation, grain, stillimage, fastdecode, zerolatency
        string profile = settings.Profile ?? "baseline"; // baseline, main, high

        int bufsize = Math.Max(bitrate, bitrate * 2);
        int gop = fps;

        string encoderArgs = $"-c:v libx264 " +
                             $"-profile:v {profile} " +
                             $"-preset {preset} " +
                             $"-tune {tune} " +
                             $"-b:v {bitrate}k " +
                             $"-maxrate {bitrate}k " +
                             $"-bufsize {bufsize}k " +
                             $"-g {gop} " +
                             $"-keyint_min {gop} " +
                             $"-sc_threshold 0 " +
                             $"-bf 0 " +
                             $"-slices 1 " +
                             $"-threads 0";

        return ("libx264", encoderArgs);
    }
}
