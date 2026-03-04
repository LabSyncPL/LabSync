namespace LabSync.Agent.Modules.RemoteDesktop.Abstractions;

public interface IVideoEncoder : IAsyncDisposable
{
    Task InitializeAsync(EncoderOptions options, CancellationToken cancellationToken = default);
    Task EncodeAsync(CaptureFrame frame, CancellationToken cancellationToken = default);
    IAsyncEnumerable<EncodedFrame> GetEncodedStreamAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(EncoderOptions options, CancellationToken cancellationToken = default);
}

public record EncoderOptions(
    int SourceWidth,
    int SourceHeight,
    int OutputWidth,
    int OutputHeight,
    int TargetBitrateKbps = 2000,
    int TargetFps = 30,
    VideoEncoderType EncoderType = VideoEncoderType.Software
);

public enum VideoEncoderType
{
    Software,
    NvidiaNvenc,
    AmdAmf,
    IntelQsv
}

public record EncodedFrame(
    byte[] Data,
    bool IsKeyFrame,
    DateTime EncodedAt
);
