import apiClient from './axiosClient';
import type { JobDto } from '../types/job';
import type { CreateJobRequest } from '../types/job';

export const deviceJobsQueryKey = (deviceId: string) => ['devices', deviceId, 'jobs'] as const;

export async function getDeviceJobs(deviceId: string): Promise<JobDto[]> {
  const { data } = await apiClient.get<JobDto[]>(`/api/devices/${deviceId}/jobs`);
  return data;
}

export async function createJob(deviceId: string, request: CreateJobRequest): Promise<JobDto> {
  const { data } = await apiClient.post<JobDto>(`/api/devices/${deviceId}/jobs`, request);
  return data;
}

export async function getJob(deviceId: string, jobId: string): Promise<JobDto> {
  const { data } = await apiClient.get<JobDto>(`/api/devices/${deviceId}/jobs/${jobId}`);
  return data;
}
