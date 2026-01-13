using System.Threading.Tasks;

namespace LabSync.Core.Interfaces
{
    /// <summary>
    /// The contract that all Agent modules (Core and Extensions) must implement.
    /// allows the Agent to load functionality dynamically.
    /// </summary>
    public interface IAgentModule
    {
        /// <summary>
        /// Unique name of the module (e.g., "ScriptExecutor", "SystemInfo").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Initializes the module resources. Called once during Agent startup.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Determines if this module can handle a specific task type.
        /// </summary>
        /// <param name="commandType">The command identifier (e.g., "Shell", "PowerShell").</param>
        /// <returns>True if the module supports this command.</returns>
        bool CanHandle(string commandType);

        /// <summary>
        /// Executes the requested logic.
        /// </summary>
        /// <param name="command">The command or script to execute.</param>
        /// <param name="args">Additional arguments.</param>
        /// <returns>A tuple containing the ExitCode and Output log.</returns>
        Task<(int ExitCode, string Output)> ExecuteAsync(string command, string args);
    }
}