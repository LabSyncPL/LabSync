using LabSync.Core.ValueObjects;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace LabSync.Core.Entities
{
    public class Job
    {
        /// <summary>
        /// Unique identifier for the job.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Foreign Key: The device responsible for executing this job.
        /// </summary>
        [Required]
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Navigation Property: Reference to the parent Device object.
        /// </summary>
        [ForeignKey(nameof(DeviceId))]
        public Device? Device { get; set; }

        /// <summary>
        /// The executable or script command (e.g., "winget", "apt-get", "powershell").
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Arguments passed to the command (e.g., "install Git -y").
        /// </summary>
        [MaxLength(2000)]
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// Current state of the execution pipeline.
        /// </summary>
        public JobStatus Status { get; set; } = JobStatus.Pending;

        /// <summary>
        /// Process exit code returned by the OS.
        /// </summary>
        public int? ExitCode { get; set; }

        /// <summary>
        /// Standard Output (stdout) and Standard Error (stderr) captured from the process.
        /// </summary>
        public string? Output { get; set; }

        /// <summary>
        /// Timestamp when the job was queued.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the execution finished (successfully or failed).
        /// </summary>
        public DateTime? FinishedAt { get; set; }
    }
}