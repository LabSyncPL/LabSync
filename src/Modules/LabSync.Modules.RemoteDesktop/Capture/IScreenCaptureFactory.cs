using LabSync.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Modules.RemoteDesktop.Capture;

public interface IScreenCaptureFactory
{
    IScreenCaptureService Create();
}
