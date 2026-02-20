/**
 * Mirrors LabSync.Core.Dto.JobDto
 */
export interface JobDto {
  id: string;
  deviceId: string;
  command: string;
  arguments: string;
  status: number;
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

export const JOB_STATUS_LABELS: Record<number, string> = {
  0: 'Pending',
  1: 'Running',
  2: 'Completed',
  3: 'Failed',
  4: 'Cancelled',
};
