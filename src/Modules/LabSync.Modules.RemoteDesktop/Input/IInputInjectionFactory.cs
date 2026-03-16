using LabSync.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Modules.RemoteDesktop.Input;

public interface IInputInjectionFactory
{
    IInputInjectionService Create();
}
