import apiClient from './axiosClient';
import type { SystemStatusResponse, SetupRequest } from '../types/system';
import type { ApiResponse } from '../types/device';

export async function getSystemStatus(): Promise<SystemStatusResponse> {
  const { data } = await apiClient.get<SystemStatusResponse>('/api/system/status');
  return data;
}

export async function completeSetup(request: SetupRequest): Promise<ApiResponse> {
  const { data } = await apiClient.post<ApiResponse>('/api/system/setup', request);
  return data;
}
