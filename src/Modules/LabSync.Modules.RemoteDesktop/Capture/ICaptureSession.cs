using System.Threading.Channels;
using LabSync.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Modules.RemoteDesktop.Capture;

public interface ICaptureSession
{
    (Task CaptureTask, Task EncodeTask) Start(
        IScreenCaptureService? capture,
        IAsyncEnumerator<CaptureFrame>? enumerator,
        CaptureFrame? firstFrame,
        IVideoEncoder encoder,
        CancellationToken cancellationToken);
}
