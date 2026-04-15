export interface DeviceGroupDeviceDto {
  id: string;
  hostname: string;
  isOnline: boolean;
}

export interface DeviceGroupDto {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
  deviceCount: number;
  devices: DeviceGroupDeviceDto[];
}

export interface CreateDeviceGroupRequest {
  name: string;
  description?: string | null;
}

export interface UpdateDeviceGroupRequest extends CreateDeviceGroupRequest {}
