using System;
using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Dto
{
    /// <summary>
    /// Payload sent by the Agent to report the execution result of a specific job.
    /// </summary>
    public class JobResultRequest
    {
        /// <summary>
        /// The unique identifier of the Job being reported.
        /// </summary>
        [Required]
        public Guid JobId { get; set; }

        /// <summary>
        /// Process exit code. 0 usually indicates success.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Console output (Standard Output + Standard Error).
        /// </summary>
        public string Output { get; set; } = string.Empty;
    }
}