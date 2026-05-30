using LabSync.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LabSync.Modules.RemoteDesktop.Configuration;

namespace LabSync.Modules.RemoteDesktop.Encoding;

/// <summary>
/// FFmpeg video encoder for Linux with support for hardware acceleration backends.
/// Supports NVIDIA NVENC, Intel QSV, VAAPI (AMD/Intel), and software encoding.
/// </summary>
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

    /// <summary>
    /// Builds FFmpeg command-line arguments for video encoding on Linux.
    /// Automatically selects the appropriate codec and preset based on EncoderOptions.EncoderType.
    /// Handles dimension normalization and intelligent filtering.
    /// </summary>
    protected override string BuildFfmpegArguments(EncoderOptions options)
    {
        // Validate and determine bitrate
        int bitrate = options.TargetBitrateKbps > 0 ? options.TargetBitrateKbps : _config.Encoding.DefaultBitrateKbps;
        if (bitrate <= 0) bitrate = 1500;

        // Validate and determine FPS
        int fps = options.TargetFps > 0 ? options.TargetFps : _config.Encoding.DefaultFps;
        if (fps <= 0) fps = 30;

        // Determine alignment based on encoder type
        // Hardware encoders (NVENC) prefer alignment of 8, software (libx264) works with 2
        int alignment = options.EncoderType == VideoEncoderType.NvidiaNvenc ? 8 : 2;

        // Normalize output resolution to nearest aligned dimensions while preserving aspect ratio
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
        var (codecName, encoderArgs) = GetEncoderSettings(options.EncoderType, bitrate, fps);

        // Construct final FFmpeg command
        return $"-hide_banner -loglevel warning " +
               $"-f rawvideo -pix_fmt bgra -s {options.SourceWidth}x{options.SourceHeight} " +
               $"-blocksize {frameSize} -r {fps} -i - " +
               $"{videoFilter} " +
               $"{encoderArgs} " +
               $"-f h264 -an -";
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
            VideoEncoderType.AmdAmf => GetVaapiSettings(bitrate, fps), // VAAPI for AMD on Linux
            VideoEncoderType.Software => GetSoftwareSettings(bitrate, fps),
            _ => GetSoftwareSettings(bitrate, fps)
        };
    }

    /// <summary>
    /// NVIDIA NVENC settings - Hardware acceleration via NVIDIA GPUs.
    /// Available on systems with NVIDIA drivers installed.
    /// </summary>
    private (string CodecName, string EncoderArgs) GetNvidiaSettings(int bitrate, int fps)
    {
        var settings = _config.Encoding.WindowsNvidia; // Reuse Windows Nvidia settings
        
        string preset = settings.Preset ?? "hp";
        string rc = settings.Rc ?? "cbr";

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
    /// Intel Quick Sync Video (QSV) settings on Linux - Hardware acceleration via Intel iGPU.
    /// Requires libva and Intel driver support.
    /// </summary>
    private (string CodecName, string EncoderArgs) GetIntelSettings(int bitrate, int fps)
    {
        var settings = _config.Encoding.WindowsIntel; // Reuse Windows Intel settings
        
        string preset = settings.Preset ?? "veryfast";
        int asyncDepth = settings.AsyncDepth > 0 ? settings.AsyncDepth : 1;

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
    /// VAAPI settings on Linux - Universal hardware acceleration for AMD and Intel GPUs.
    /// Requires libva library and appropriate GPU drivers (libva-amdgpu or libva-intel-driver).
    /// </summary>
    private (string CodecName, string EncoderArgs) GetVaapiSettings(int bitrate, int fps)
    {
        int bufsize = Math.Max(bitrate, bitrate * 2);
        int gop = fps;

        // VAAPI encoder with quality/speed tradeoff
        string encoderArgs = $"-c:v h264_vaapi " +
                             $"-qp 25 " +
                             $"-b:v {bitrate}k " +
                             $"-maxrate {bitrate}k " +
                             $"-bufsize {bufsize}k " +
                             $"-g {gop}";

        return ("h264_vaapi", encoderArgs);
    }

    /// <summary>
    /// Software encoding via libx264 - Fallback for systems without hardware acceleration.
    /// Always available but trades CPU usage for maximum compatibility.
    /// </summary>
    private (string CodecName, string EncoderArgs) GetSoftwareSettings(int bitrate, int fps)
    {
        var settings = _config.Encoding.LinuxSoftware;
        
        string preset = settings.Preset ?? "ultrafast";
        string tune = settings.Tune ?? "zerolatency";
        string profile = settings.Profile ?? "baseline";

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
