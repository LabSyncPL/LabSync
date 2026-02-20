/**
 * Mirrors LabSync.Core.ValueObjects.DevicePlatform
 */
export const DevicePlatform = {
  Unknown: 0,
  Windows: 1,
  Linux: 2,
  MacOS: 3,
} as const;

export type DevicePlatform = (typeof DevicePlatform)[keyof typeof DevicePlatform];

/**
 * Mirrors LabSync.Core.ValueObjects.DeviceStatus
 */
export const DeviceStatus = {
  Pending: 0,
  Active: 1,
  Maintenance: 2,
  Blocked: 3,
} as const;

export type DeviceStatus = (typeof DeviceStatus)[keyof typeof DeviceStatus];

/**
 * Mirrors LabSync.Core.Dto.DeviceDto
 */
export interface DeviceDto {
  id: string;
  hostname: string;
  isApproved: boolean;
  macAddress: string;
  ipAddress: string | null;
  platform: DevicePlatform;
  osVersion: string;
  status: DeviceStatus;
  isOnline: boolean;
  registeredAt: string;
  lastSeenAt: string | null;
  groupId: string | null;
  groupName: string | null;
  hardwareInfo: string | null;
}

/**
 * Mirrors LabSync.Core.Dto.ApiResponse
 */
export interface ApiResponse {
  message: string;
}

export const DEVICE_PLATFORM_LABELS: Record<DevicePlatform, string> = {
  [DevicePlatform.Unknown]: 'Unknown',
  [DevicePlatform.Windows]: 'Windows',
  [DevicePlatform.Linux]: 'Linux',
  [DevicePlatform.MacOS]: 'MacOS',
};

export const DEVICE_STATUS_LABELS: Record<DeviceStatus, string> = {
  [DeviceStatus.Pending]: 'Pending',
  [DeviceStatus.Active]: 'Active',
  [DeviceStatus.Maintenance]: 'Maintenance',
  [DeviceStatus.Blocked]: 'Blocked',
};
