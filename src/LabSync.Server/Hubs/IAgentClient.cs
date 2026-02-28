namespace LabSync.Server.Hubs;

public interface IAgentClient
{
    Task ReceiveJob(Guid jobId, string command, string arguments, string? scriptPayload);
    Task Ping();
}