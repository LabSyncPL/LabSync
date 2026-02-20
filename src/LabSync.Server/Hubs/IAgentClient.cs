using LabSync.Core.Dto;

namespace LabSync.Server.Hubs
{
    /// <summary>
    /// Defines the client-side methods that the server hub can invoke.
    /// This creates a strongly-typed contract for server-to-client communication.
    /// </summary>
    public interface IAgentClient
    {
        /// <summary>
        /// Instructs the agent to execute a new job.
        /// </summary>
        /// <param name="jobId">The unique ID of the job to execute.</param>
        /// <param name="command">The command or job type (e.g., "Run-Script", "Get-SysInfo").</param>
        /// <param name="arguments">The arguments for the command.</param>
        Task ReceiveJob(Guid jobId, string command, string arguments);
    }
}
