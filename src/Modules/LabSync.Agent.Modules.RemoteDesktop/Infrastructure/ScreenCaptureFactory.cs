using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Agent.Modules.RemoteDesktop.Capture;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace LabSync.Agent.Modules.RemoteDesktop.Infrastructure;

public static class ScreenCaptureFactory
{
    public static IScreenCaptureFactory Create(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
            ? factory.CreateLogger<PlaceholderScreenCaptureService>()
            : null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsScreenCaptureFactory(logger);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxScreenCaptureFactory(logger);

        return new PlaceholderScreenCaptureFactory(logger);
    }
}

internal sealed class WindowsScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;

    public WindowsScreenCaptureFactory(ILogger? logger) => _logger = logger;

    public IScreenCaptureService Create() => new PlaceholderScreenCaptureService(_logger);
}

internal sealed class LinuxScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;

    public LinuxScreenCaptureFactory(ILogger? logger) => _logger = logger;

    public IScreenCaptureService Create() => new PlaceholderScreenCaptureService(_logger);
}

internal sealed class PlaceholderScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;

    public PlaceholderScreenCaptureFactory(ILogger? logger) => _logger = logger;

    public IScreenCaptureService Create() => new PlaceholderScreenCaptureService(_logger);
}

internal sealed class PlaceholderScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger? _logger;

    public PlaceholderScreenCaptureService(ILogger? logger) => _logger = logger;

    public Task StartCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(33, cancellationToken);
            yield return new CaptureFrame(Array.Empty<byte>(), 1920, 1080, 7680, PixelFormat.Bgra32, DateTime.UtcNow);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
