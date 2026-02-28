namespace LabSync.Agent.Modules.RemoteDesktop.Abstractions;

public interface IVideoEncoder : IAsyncDisposable
{
    Task InitializeAsync(EncoderOptions options, CancellationToken cancellationToken = default);
    Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default);
    IAsyncEnumerable<EncodedFrame> GetEncodedStreamAsync(CancellationToken cancellationToken = default);
}

public record EncoderOptions(
    int Width,
    int Height,
    int TargetBitrateKbps = 2000,
    int TargetFps = 30
);

public record EncodedFrame(
    byte[] Data,
    bool IsKeyFrame,
    DateTime EncodedAt
);
