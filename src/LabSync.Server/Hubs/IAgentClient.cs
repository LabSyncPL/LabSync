namespace LabSync.Server.Hubs;

/// <summary>
/// Defines the client-side methods that the server can invoke on connected agents.
/// Strongly-typed contract for server-to-agent communication.
/// </summary>
public interface IAgentClient
{
    /// <summary>
    /// Sends a job to the agent for execution. Agent must execute and call UploadJobResult.
    /// </summary>
    Task ReceiveJob(Guid jobId, string command, string arguments);

    /// <summary>
    /// Optional: server can request a heartbeat response to verify the agent is still alive.
    /// </summary>
    Task Ping();
}

