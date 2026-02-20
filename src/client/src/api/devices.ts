import apiClient from './axiosClient';
import type { DeviceDto, ApiResponse } from '../types/device';

export const devicesQueryKey = ['devices'] as const;

export async function fetchDevices(): Promise<DeviceDto[]> {
  const { data } = await apiClient.get<DeviceDto[]>('/api/devices');
  return data;
}

export async function approveDevice(deviceId: string): Promise<ApiResponse> {
  const { data } = await apiClient.post<ApiResponse>(`/api/devices/${deviceId}/approve`);
  return data;
}
