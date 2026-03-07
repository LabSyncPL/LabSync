using LabSync.Agent.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Agent.Modules.RemoteDesktop.Capture;

public interface IScreenCaptureFactory
{
    IScreenCaptureService Create();
}
