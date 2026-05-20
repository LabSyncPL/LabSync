using LabSync.Modules.RemoteDesktop.Abstractions;
using LabSync.Modules.RemoteDesktop.Capture;
using LabSync.Modules.RemoteDesktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LabSync.Modules.RemoteDesktop.Infrastructure;

public class ScreenCaptureFactorySelector : IScreenCaptureFactory
{
    private readonly ILogger<ScreenCaptureFactorySelector> _logger;
    private readonly IOptions<RemoteDesktopConfiguration> _options;

    public ScreenCaptureFactorySelector(
        ILogger<ScreenCaptureFactorySelector> logger,
        IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _options = options;
    }

    public IScreenCaptureService Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsFfmpegScreenCaptureService(_logger, _options);
        if (OperatingSystem.IsLinux())
            return new LinuxFfmpegScreenCaptureService(_logger, _options);

        return new PlaceholderScreenCaptureService(_logger, _options);
    }
}

internal abstract class FfmpegScreenCaptureServiceBase : IScreenCaptureService
{
    protected readonly ILogger _logger;
    protected readonly RemoteDesktopConfiguration _config;
    private readonly string _ffmpegPath;
    private Process? _process;
    private Task? _stderrTask;
    private bool _started;
    private bool _disposed;
    private int _sourceWidth;
    private int _sourceHeight;

    protected FfmpegScreenCaptureServiceBase(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _config = options.Value;
        _ffmpegPath = string.IsNullOrWhiteSpace(_config.Encoding.FfmpegPath) ? "ffmpeg" : _config.Encoding.FfmpegPath;
    }

    protected string PixelFormat => string.IsNullOrWhiteSpace(_config.Capture.PixelFormat) ? "bgra" : _config.Capture.PixelFormat;
    protected int BytesPerPixel => PixelFormat.ToLowerInvariant() switch
    {
        "bgra" => 4,
        "rgba" => 4,
        _ => 4
    };

    public async Task StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        if (_started) return;

        var resolution = await ResolveCaptureResolutionAsync(cancellationToken);
        _sourceWidth = resolution.Width;
        _sourceHeight = resolution.Height;

        await StartProcessAsync(cancellationToken);
        _started = true;
    }

    public Task StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        CleanupProcess();
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            await StartCaptureAsync(cancellationToken).ConfigureAwait(false);
        }

        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            CaptureFrame? frame;
            try
            {
                frame = await ReadNextFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FFmpeg screen capture failed. Restarting ffmpeg.");
                await RestartProcessAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (frame == null)
            {
                await RestartProcessAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            yield return frame;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupProcess();
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }

    private async Task StartProcessAsync(CancellationToken cancellationToken)
    {
        CleanupProcess();

        var args = BuildFfmpegArguments(_sourceWidth, _sourceHeight, _config.Capture.TargetFps);
        _logger.LogInformation("Starting FFmpeg capture process: {Path} {Args}", _ffmpegPath, args);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg process.");

            _stderrTask = Task.Run(async () =>
            {
                try
                {
                    while (!_process.HasExited)
                    {
                        var line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        if (line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("fail", StringComparison.OrdinalIgnoreCase) || line.Contains("panic", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("ffmpeg stderr: {Line}", line);
                        }
                        else
                        {
                            _logger.LogDebug("ffmpeg stderr: {Line}", line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error reading ffmpeg stderr.");
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FFmpeg capture process.");
            throw;
        }

        await Task.CompletedTask;
    }

    private async Task<CaptureFrame?> ReadNextFrameAsync(CancellationToken cancellationToken)
    {
        if (_process == null || _process.HasExited)
        {
            return null;
        }

        var stdout = _process.StandardOutput.BaseStream;
        int frameSize = _sourceWidth * _sourceHeight * BytesPerPixel;
        var buffer = ArrayPool<byte>.Shared.Rent(frameSize);
        try
        {
            int read = 0;
            while (read < frameSize)
            {
                int chunk = await stdout.ReadAsync(buffer.AsMemory(read, frameSize - read), cancellationToken).ConfigureAwait(false);
                if (chunk == 0)
                {
                    return null;
                }
                read += chunk;
            }

            var data = new byte[frameSize];
            Buffer.BlockCopy(buffer, 0, data, 0, frameSize);
            return new CaptureFrame(data, _sourceWidth, _sourceHeight, _sourceWidth * BytesPerPixel, PixelFormat == "rgba" ? Abstractions.PixelFormat.Rgba32 : Abstractions.PixelFormat.Bgra32, DateTime.UtcNow);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task RestartProcessAsync(CancellationToken cancellationToken)
    {
        CleanupProcess();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        await StartProcessAsync(cancellationToken).ConfigureAwait(false);
    }

    private void CleanupProcess()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                    _process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error terminating ffmpeg process.");
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    private async Task<(int Width, int Height)> ResolveCaptureResolutionAsync(CancellationToken cancellationToken)
    {
        if (_config.Capture.CaptureWidth > 0 && _config.Capture.CaptureHeight > 0)
        {
            return (_config.Capture.CaptureWidth, _config.Capture.CaptureHeight);
        }

        return await GetScreenResolutionAsync(cancellationToken).ConfigureAwait(false);
    }

    protected abstract string BuildFfmpegArguments(int width, int height, int fps);
    protected abstract Task<(int Width, int Height)> GetScreenResolutionAsync(CancellationToken cancellationToken);
}

internal sealed class WindowsFfmpegScreenCaptureService : FfmpegScreenCaptureServiceBase
{
    public WindowsFfmpegScreenCaptureService(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
        : base(logger, options)
    {
    }

    protected override string BuildFfmpegArguments(int width, int height, int fps)
    {
        var mode = _config.Capture.WindowsCaptureMode?.ToLowerInvariant() ?? "gdigrab";
        if (mode != "d3d11grab") mode = "gdigrab";

        var captureArgs = mode == "d3d11grab"
            ? $"-f d3d11grab -framerate {fps} -i desktop"
            : $"-f gdigrab -framerate {fps} -offset_x 0 -offset_y 0 -video_size {width}x{height} -i desktop";

        return $"-hide_banner -loglevel warning {captureArgs} -pix_fmt {PixelFormat} -f rawvideo pipe:1";
    }

    protected override Task<(int Width, int Height)> GetScreenResolutionAsync(CancellationToken cancellationToken)
    {
        int width = GetSystemMetrics(0);
        int height = GetSystemMetrics(1);

        if (width <= 0 || height <= 0)
        {
            width = 1920;
            height = 1080;
        }

        return Task.FromResult((width, height));
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}

internal sealed class LinuxFfmpegScreenCaptureService : FfmpegScreenCaptureServiceBase
{
    public LinuxFfmpegScreenCaptureService(ILogger? logger, IOptions<RemoteDesktopConfiguration> options)
        : base(logger, options)
    {
    }

    protected override string BuildFfmpegArguments(int width, int height, int fps)
    {
        var display = _config.Capture.X11Display;
        if (string.IsNullOrWhiteSpace(display))
        {
            display = Environment.GetEnvironmentVariable("DISPLAY") ?? ":0.0";
        }

        if (_config.Capture.UseWaylandPipewire)
        {
            var source = _config.Capture.PipewireSource ?? "0";
            return $"-hide_banner -loglevel warning -f pipewire -framerate {fps} -i {source} -pix_fmt {PixelFormat} -f rawvideo pipe:1";
        }

        return $"-hide_banner -loglevel warning -f x11grab -framerate {fps} -video_size {width}x{height} -i {display} -pix_fmt {PixelFormat} -f rawvideo pipe:1";
    }

    protected override Task<(int Width, int Height)> GetScreenResolutionAsync(CancellationToken cancellationToken)
    {
        return LinuxDisplayHelper.GetScreenResolutionAsync(cancellationToken);
    }
}

internal sealed class PlaceholderScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger _logger;
    private readonly RemoteDesktopConfiguration _config;

    public PlaceholderScreenCaptureService(ILogger logger, IOptions<RemoteDesktopConfiguration> options)
    {
        _logger = logger;
        _config = options.Value;
    }

    public Task StartCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopCaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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
