import apiClient from "./axiosClient";
import type {
  CreateDeviceGroupRequest,
  DeviceGroupDto,
  UpdateDeviceGroupRequest,
} from "../types/deviceGroups";

export const deviceGroupsQueryKey = ["device-groups"] as const;

export async function fetchDeviceGroups(): Promise<DeviceGroupDto[]> {
  const { data } = await apiClient.get<DeviceGroupDto[]>("/api/device-groups");
  return data;
}

export async function createDeviceGroup(payload: CreateDeviceGroupRequest): Promise<DeviceGroupDto> {
  const { data } = await apiClient.post<DeviceGroupDto>("/api/device-groups", payload);
  return data;
}

export async function updateDeviceGroup(
  id: string,
  payload: UpdateDeviceGroupRequest,
): Promise<DeviceGroupDto> {
  const { data } = await apiClient.put<DeviceGroupDto>(`/api/device-groups/${id}`, payload);
  return data;
}

export async function deleteDeviceGroup(id: string): Promise<void> {
  await apiClient.delete(`/api/device-groups/${id}`);
}
