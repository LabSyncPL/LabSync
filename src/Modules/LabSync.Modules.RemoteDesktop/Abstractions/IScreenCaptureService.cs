namespace LabSync.Modules.RemoteDesktop.Abstractions;

public interface IScreenCaptureService : IAsyncDisposable
{
    Task StartCaptureAsync(CancellationToken cancellationToken = default);
    Task StopCaptureAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<CaptureFrame> EnumerateFramesAsync(CancellationToken cancellationToken = default);
}

public record CaptureFrame(
    byte[] Data,
    int Width,
    int Height,
    int Stride,
    PixelFormat Format,
    DateTime CapturedAt
);

public enum PixelFormat
{
    Bgra32,
    Rgba32
}
