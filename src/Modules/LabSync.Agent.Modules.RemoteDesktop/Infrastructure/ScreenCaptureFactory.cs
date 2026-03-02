using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Agent.Modules.RemoteDesktop.Capture;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

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

    public IScreenCaptureService Create() => new WindowsScreenCaptureService(_logger);
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
            yield return new CaptureFrame(Array.Empty<byte>(), 1920, 1080, 7680, Abstractions.PixelFormat.Bgra32, DateTime.UtcNow);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger? _logger;
    private bool _disposed;

    public WindowsScreenCaptureService(ILogger? logger)
    {
        _logger = logger;
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
        const int targetFps = 20;
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
