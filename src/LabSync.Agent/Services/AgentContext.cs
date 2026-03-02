using LabSync.Core.Interfaces;

namespace LabSync.Agent.Services;

public class AgentContext : IAgentContext
{
    public Guid DeviceId { get; private set; }

    public void SetDeviceId(Guid deviceId)
    {
        DeviceId = deviceId;
    }
}
