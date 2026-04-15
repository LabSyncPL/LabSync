import apiClient from "./axiosClient";
import type { DeviceDto, ApiResponse } from "../types/device";

export const devicesQueryKey = ["devices"] as const;

export async function fetchDevices(): Promise<DeviceDto[]> {
  const { data } = await apiClient.get<DeviceDto[]>("/api/devices");
  return data;
}

export async function approveDevice(deviceId: string): Promise<ApiResponse> {
  const { data } = await apiClient.post<ApiResponse>(
    `/api/devices/${deviceId}/approve`,
  );
  return data;
}

export async function setSshCredentials(
  deviceId: string,
  credentials: {
    username: string;
    password?: string;
    privateKey?: string;
    useKeyAuthentication?: boolean;
  },
): Promise<ApiResponse> {
  const { data } = await apiClient.post<ApiResponse>(
    `/api/devices/${deviceId}/credentials`,
    credentials,
  );
  return data;
}

export async function assignDeviceToGroup(
  deviceId: string,
  groupId: string,
): Promise<ApiResponse> {
  const { data } = await apiClient.post<ApiResponse>(`/api/devices/${deviceId}/group`, {
    groupId,
  });
  return data;
}

export async function removeDeviceFromGroup(deviceId: string): Promise<ApiResponse> {
  const { data } = await apiClient.delete<ApiResponse>(`/api/devices/${deviceId}/group`);
  return data;
}
