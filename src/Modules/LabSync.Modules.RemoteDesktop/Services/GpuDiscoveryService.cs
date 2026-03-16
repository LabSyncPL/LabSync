using System.Diagnostics;
using LabSync.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.RemoteDesktop.Services;

public interface IGpuDiscoveryService
{
    Task<List<VideoEncoderType>> GetAvailableEncodersAsync(CancellationToken cancellationToken = default);
}

public class GpuDiscoveryService : IGpuDiscoveryService
{
    private readonly ILogger<GpuDiscoveryService> _logger;
    private readonly string _ffmpegPath;

    public GpuDiscoveryService(ILogger<GpuDiscoveryService> logger, string ffmpegPath = "ffmpeg")
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
    }

    public async Task<List<VideoEncoderType>> GetAvailableEncodersAsync(CancellationToken cancellationToken = default)
    {
        var available = new List<VideoEncoderType> { VideoEncoderType.Software };

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-hide_banner -encoders",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogWarning("Failed to start ffmpeg for capability check.");
                return available;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask  = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            
            var output = await outputTask;
            var error  = await errorTask;
            var combined = output + Environment.NewLine + error;

            if (combined.Contains("h264_nvenc"))
            {
                available.Add(VideoEncoderType.NvidiaNvenc);
            }
            if (combined.Contains("h264_amf"))
            {
                available.Add(VideoEncoderType.AmdAmf);
            }
            if (combined.Contains("h264_qsv"))
            {
                available.Add(VideoEncoderType.IntelQsv);
            }
            
            _logger.LogInformation("Detected encoders: {Encoders}", string.Join(", ", available));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting GPU capabilities.");
        }

        return available;
    }
}
