using System.Threading.Channels;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Agent.Modules.RemoteDesktop.Capture;

public interface ICaptureSession
{
    (Task CaptureTask, Task EncodeTask) Start(
        IScreenCaptureService? capture,
        IAsyncEnumerator<CaptureFrame>? enumerator,
        CaptureFrame? firstFrame,
        IVideoEncoder encoder,
        int captureChannelCapacity,
        CancellationToken cancellationToken);
}
