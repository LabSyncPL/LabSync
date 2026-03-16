using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Agent.Modules.RemoteDesktop.Encoding;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace LabSync.Agent.Modules.RemoteDesktop.Infrastructure;

public interface IVideoEncoderFactory
{
    IVideoEncoder Create(int channelCapacity);
}

public class VideoEncoderFactory : IVideoEncoderFactory
{
    private readonly ILogger<VideoEncoderFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public VideoEncoderFactory(ILogger<VideoEncoderFactory> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public IVideoEncoder Create(int channelCapacity)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsFfmpegVideoEncoder(_loggerFactory.CreateLogger<WindowsFfmpegVideoEncoder>(), channelCapacity);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxFfmpegVideoEncoder(_loggerFactory.CreateLogger<LinuxFfmpegVideoEncoder>(), channelCapacity);
        }
        return new PlaceholderVideoEncoder(_loggerFactory.CreateLogger<PlaceholderVideoEncoder>(), channelCapacity);
    }
}
