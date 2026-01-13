namespace LabSync.Core.ValueObjects
{
    /// <summary>
    /// Represents the lifecycle of an execution task.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>
        /// Task created in database but not yet picked up by the Agent.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Agent has received the task and is currently executing it.
        /// </summary>
        Running = 1,

        /// <summary>
        /// Execution finished successfully (usually ExitCode 0).
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Execution failed (non-zero ExitCode) or timed out.
        /// </summary>
        Failed = 3,

        /// <summary>
        /// Task was cancelled by user before completion.
        /// </summary>
        Cancelled = 4
    }
}
