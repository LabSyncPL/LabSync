export const JobStatus = {
  Pending: 0,
  Running: 1,
  Completed: 2,
  Failed: 3,
  Cancelled: 4,
} as const;

export type JobStatus = (typeof JobStatus)[keyof typeof JobStatus];

export interface JobDto {
  id: string;
  deviceId: string;
  command: string;
  arguments: string;
  status: JobStatus;
  exitCode: number | null;
  output: string | null;
  createdAt: string;
  finishedAt: string | null;
}

/**
 * Mirrors LabSync.Core.Dto.CreateJobRequest
 */
export interface CreateJobRequest {
  command: string;
  arguments: string;
  scriptPayload?: string | null;
}

export const JOB_STATUS_LABELS: Record<JobStatus, string> = {
  [JobStatus.Pending]: 'Pending',
  [JobStatus.Running]: 'Running',
  [JobStatus.Completed]: 'Completed',
  [JobStatus.Failed]: 'Failed',
  [JobStatus.Cancelled]: 'Cancelled',
};
