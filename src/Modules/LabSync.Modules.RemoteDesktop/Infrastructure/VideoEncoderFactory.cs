using LabSync.Modules.RemoteDesktop.Abstractions;
using LabSync.Modules.RemoteDesktop.Encoding;
using LabSync.Modules.RemoteDesktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace LabSync.Modules.RemoteDesktop.Infrastructure;

public interface IVideoEncoderFactory
{
    IVideoEncoder Create();
}

public class VideoEncoderFactory : IVideoEncoderFactory
{
    private readonly ILogger<VideoEncoderFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public VideoEncoderFactory(
        ILogger<VideoEncoderFactory> logger, 
        ILoggerFactory loggerFactory,
        IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _options = options;
    }

    public IVideoEncoder Create()
    {
        int capacity = _options.Value.Encoding.EncodedChannelCapacity;
        if (capacity <= 0) capacity = 2;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsFfmpegVideoEncoder(_loggerFactory.CreateLogger<WindowsFfmpegVideoEncoder>(), _options, capacity);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxFfmpegVideoEncoder(_loggerFactory.CreateLogger<LinuxFfmpegVideoEncoder>(), _options, capacity);
        }
        return new PlaceholderVideoEncoder(_loggerFactory.CreateLogger<PlaceholderVideoEncoder>(), capacity);
    }
}
