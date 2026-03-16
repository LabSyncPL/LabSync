using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Agent.Modules.RemoteDesktop.Capture;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

using LabSync.Agent.Modules.RemoteDesktop.Configuration;
using Microsoft.Extensions.Options;

namespace LabSync.Agent.Modules.RemoteDesktop.Infrastructure;

public class ScreenCaptureFactorySelector : IScreenCaptureFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public ScreenCaptureFactorySelector(
        IServiceProvider serviceProvider, 
        ILogger<ScreenCaptureFactorySelector> logger,
        IOptions<RemoteDesktopConfiguration> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    public IScreenCaptureService Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsScreenCaptureFactory(_logger, _options).Create();
        if (OperatingSystem.IsLinux())
            return new LinuxScreenCaptureFactory(_logger).Create();

        return new PlaceholderScreenCaptureFactory(_logger, _options).Create();
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public WindowsScreenCaptureFactory(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _options = options;
    }

    public IScreenCaptureService Create() => new WindowsScreenCaptureService(_logger, _options);
}

internal sealed class LinuxScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;

    public LinuxScreenCaptureFactory(ILogger? logger) => _logger = logger;

    public IScreenCaptureService Create() => new PlaceholderScreenCaptureService(_logger, null);
}

internal sealed class PlaceholderScreenCaptureFactory : IScreenCaptureFactory
{
    private readonly ILogger? _logger;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public PlaceholderScreenCaptureFactory(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _options = options;
    }

    public IScreenCaptureService Create() => new PlaceholderScreenCaptureService(_logger, _options);
}

internal sealed class PlaceholderScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger? _logger;
    private readonly RemoteDesktopConfiguration _config;

    public PlaceholderScreenCaptureService(ILogger? logger, IOptions<RemoteDesktopConfiguration>? options)
    {
        _logger = logger;
        _config = options?.Value ?? new RemoteDesktopConfiguration();
    }

    public Task StartCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int delay = _config.Capture.PlaceholderDelayMs;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(delay, cancellationToken);
            yield return new CaptureFrame(Array.Empty<byte>(), 1920, 1080, 7680, Abstractions.PixelFormat.Bgra32, DateTime.UtcNow);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger? _logger;
    private readonly RemoteDesktopConfiguration _config;
    private bool _disposed;

    public WindowsScreenCaptureService(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _config = options.Value;
    }

    public Task StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int targetFps = _config.Capture.TargetFps;
        if (targetFps <= 0) targetFps = 20;
        
        var frameDelay = TimeSpan.FromMilliseconds(1000.0 / targetFps);

        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            var started = DateTime.UtcNow;

            CaptureFrame? frame = null;
            try
            {
                using var bmp = CapturePrimaryScreen();
                if (bmp is not null)
                {
                    frame = ConvertToCaptureFrame(bmp, started);
                }
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Windows screen capture failed.");
            }

            if (frame is not null)
            {
                yield return frame;
            }

            var elapsed = DateTime.UtcNow - started;
            var delay = frameDelay - elapsed;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
            }
        }
    }

    private static Bitmap? CapturePrimaryScreen()
    {
        var width = GetSystemMetrics(0);  // SM_CXSCREEN
        var height = GetSystemMetrics(1); // SM_CYSCREEN
        if (width <= 0 || height <= 0)
            return null;

        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        return bmp;
    }

    private static CaptureFrame ConvertToCaptureFrame(Bitmap bitmap, DateTime capturedAt)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var length = stride * data.Height;
            var buffer = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, length);
            return new CaptureFrame(buffer, data.Width, data.Height, stride, Abstractions.PixelFormat.Bgra32, capturedAt);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
