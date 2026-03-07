using LabSync.Agent.Modules.RemoteDesktop.Abstractions;

namespace LabSync.Agent.Modules.RemoteDesktop.Input;

public interface IInputInjectionFactory
{
    IInputInjectionService Create();
}
