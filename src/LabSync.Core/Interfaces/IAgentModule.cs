using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LabSync.Core.Interfaces
{
    /// <summary>
    /// Represents a dynamic plugin capable of performing specific agent tasks.
    /// </summary>
    public interface IAgentModule
    {
        /// <summary>
        /// Unique identifier of the module (e.g., "SystemMonitor", "ScriptExecutor").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Module version for compatibility checks (e.g., "1.0.0").
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the module with necessary services
        /// </summary>
        /// <param name="serviceProvider">Access to Agent's DI container.</param>
        Task InitializeAsync(IServiceProvider serviceProvider);

        /// <summary>
        /// Checks if the module can handle a specific job type.
        /// </summary>
        /// <param name="jobType">The type of job (e.g., "Get-SysInfo", "Run-PowerShell").</param>
        bool CanHandle(string jobType);

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <param name="parameters">Dictionary of arguments specific to the job type.</param>
        /// <param name="cancellationToken">Token to cancel long-running operations.</param>
        /// <returns>Generic result object containing success status and data/logs.</returns>
        Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Standardized result returned by any module.
    /// </summary>
    public class ModuleResult
    {
        public bool IsSuccess { get; set; }
        public object? Data { get; set; } // JSON, Text, or Binary data
        public string? ErrorMessage { get; set; }

        public static ModuleResult Success(object? data = null) => new() { IsSuccess = true, Data = data };
        public static ModuleResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
    }
}