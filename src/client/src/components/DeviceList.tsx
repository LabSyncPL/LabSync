import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  fetchDevices,
  approveDevice,
  devicesQueryKey,
} from '../api/devices';
import type { DeviceDto } from '../types/device';
import { DEVICE_PLATFORM_LABELS, DEVICE_STATUS_LABELS } from '../types/device';
import styles from './DeviceList.module.css';

function formatLastSeen(value: string | null): string {
  if (value == null) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

export function DeviceList() {
  const queryClient = useQueryClient();

  const {
    data: devices = [],
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
  });

  const approveMutation = useMutation({
    mutationFn: approveDevice,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
    },
  });

  const handleApprove = (device: DeviceDto) => {
    if (device.isApproved) return;
    approveMutation.mutate(device.id, {
      onError: () => {
        alert('Failed to approve device.');
      },
    });
  };

  if (isLoading) return <p className={styles.loading}>Loading devices…</p>;
  if (isError) {
    return (
      <div className={styles.error}>
        Failed to load devices. {error instanceof Error ? error.message : 'Unknown error.'}
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h2 className={styles.title}>Devices (LabSync)</h2>
        <button type="button" className={styles.refreshBtn} onClick={() => refetch()}>
          Refresh
        </button>
      </div>

      {devices.length === 0 ? (
        <p className={styles.noData}>No devices registered.</p>
      ) : (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Hostname</th>
              <th>MAC Address</th>
              <th>IP</th>
              <th>Platform</th>
              <th>OS Version</th>
              <th>Last seen</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {devices.map((device) => (
              <tr
                key={device.id}
                className={device.isApproved ? styles.approved : styles.pendingApproval}
              >
                <td><strong>{device.hostname}</strong></td>
                <td className={styles.macAddress}>{device.macAddress}</td>
                <td>{device.ipAddress ?? '—'}</td>
                <td>{DEVICE_PLATFORM_LABELS[device.platform]}</td>
                <td>{device.osVersion}</td>
                <td className={styles.lastSeen}>{formatLastSeen(device.lastSeenAt)}</td>
                <td>
                  <div className={styles.statusCell}>
                    <span
                      className={`${styles.lifecycleStatus} ${styles[DEVICE_STATUS_LABELS[device.status].toLowerCase()]}`}
                    >
                      {DEVICE_STATUS_LABELS[device.status]}
                    </span>
                    <span
                      className={`${styles.connectionStatus} ${device.isOnline ? styles.online : styles.offline}`}
                    >
                      {device.isOnline ? 'Online' : 'Offline'}
                    </span>
                  </div>
                </td>
                <td style={{ textAlign: 'center' }}>
                  {!device.isApproved ? (
                    <button
                      type="button"
                      className={styles.approveBtn}
                      onClick={() => handleApprove(device)}
                      disabled={approveMutation.isPending}
                    >
                      Approve
                    </button>
                  ) : (
                    <span className={styles.approvedLabel}>Approved</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
