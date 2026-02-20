import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { fetchDevices, approveDevice, devicesQueryKey } from '../api/devices';
import { clearToken } from '../auth/authStore';
import { useNavigate } from 'react-router-dom';
import type { DeviceDto } from '../types/device';

function formatLastSeen(value: string | null): string {
  if (value == null) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins} min ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  return date.toLocaleDateString();
}

function getPlatformIcon(platform: number) {
  switch (platform) {
    case 1: // Windows
      return (
        <svg className="w-4 h-4 text-blue-400" viewBox="0 0 24 24" fill="currentColor">
          <path d="M0 3.449L9.75 2.1v9.451H0m10.949-9.602L24 0v11.4h-13.051M0 12.6h9.75v9.451L0 20.699M10.949 12.6H24V24l-12.9-1.801" />
        </svg>
      );
    case 2: // Linux
      return (
        <svg className="w-4 h-4 text-yellow-500" viewBox="0 0 24 24" fill="currentColor">
          <path d="M12 0c-6.627 0-12 5.373-12 12s5.373 12 12 12 12-5.373 12-12-5.373-12-12-12zm4.333 3.667c.736 0 1.333.597 1.333 1.333 0 .736-.597 1.333-1.333 1.333-.736 0-1.333-.597-1.333-1.333 0-.736.597-1.333 1.333-1.333zm-8.667 0c.736 0 1.333.597 1.333 1.333 0 .736-.597 1.333-1.333 1.333-.736 0-1.333-.597-1.333-1.333 0-.736.597-1.333 1.333-1.333zm9.056 12.333h-1.333v2.667h-2.667v-2.667h-1.333v2.667h-2.667v-2.667h-1.389v-4h10.778v4z" />
        </svg>
      );
    default:
      return null;
  }
}

export function Dashboard() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: devices = [], isLoading, refetch } = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
    refetchInterval: 30000,
  });

  const approveMutation = useMutation({
    mutationFn: approveDevice,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
    },
  });

  const handleApprove = (e: React.MouseEvent, device: DeviceDto) => {
    e.stopPropagation();
    if (device.isApproved) return;
    approveMutation.mutate(device.id, {
      onError: () => {
        alert('Failed to approve device.');
      },
    });
  };

  const totalDevices = devices.length;
  const onlineDevices = devices.filter((d) => d.isOnline).length;
  const pendingDevices = devices.filter((d) => !d.isApproved).length;

  const handleLogout = () => {
    clearToken();
    navigate('/login');
  };

  return (
    <>
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <h1 className="text-xl font-semibold text-white">Overview</h1>
        <div className="flex items-center space-x-4">
          <button
            onClick={handleLogout}
            className="text-slate-400 hover:text-white px-3 py-1.5 rounded-lg text-sm transition-colors"
          >
            Log out
          </button>
        </div>
      </header>

      <div className="flex-1 p-8 overflow-y-auto">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <div className="bg-slate-800 p-6 rounded-xl border border-slate-700">
            <div className="flex justify-between items-start">
              <div>
                <p className="text-slate-400 text-xs font-medium uppercase">Total Devices</p>
                <h3 className="text-3xl font-bold text-white mt-1">{totalDevices}</h3>
              </div>
              <div className="p-2 bg-slate-700 rounded-lg">
                <svg className="w-6 h-6 text-primary-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"></path>
                </svg>
              </div>
            </div>
          </div>

          <div className="bg-slate-800 p-6 rounded-xl border border-slate-700">
            <div className="flex justify-between items-start">
              <div>
                <p className="text-slate-400 text-xs font-medium uppercase">Online Now</p>
                <h3 className="text-3xl font-bold text-success mt-1">{onlineDevices}</h3>
              </div>
              <div className="p-2 bg-slate-700 rounded-lg">
                <svg className="w-6 h-6 text-success" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5.636 18.364a9 9 0 010-12.728m12.728 0a9 9 0 010 12.728m-9.9-2.829a5 5 0 010-7.07m7.072 0a5 5 0 010 7.07M13 12a1 1 0 11-2 0 1 1 0 012 0z"></path>
                </svg>
              </div>
            </div>
          </div>

          <div className={`bg-slate-800 p-6 rounded-xl border ${pendingDevices > 0 ? 'border-warning/20' : 'border-slate-700'}`}>
            <div className="flex justify-between items-start">
              <div>
                <p className={`text-xs font-medium uppercase ${pendingDevices > 0 ? 'text-warning' : 'text-slate-400'}`}>
                  Pending Approval
                </p>
                <h3 className="text-3xl font-bold text-white mt-1">{pendingDevices}</h3>
              </div>
              <div className={`p-2 rounded-lg ${pendingDevices > 0 ? 'bg-warning/10' : 'bg-slate-700'}`}>
                <svg className={`w-6 h-6 ${pendingDevices > 0 ? 'text-warning' : 'text-slate-500'}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path>
                </svg>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-700 flex justify-between items-center">
            <h2 className="font-semibold text-white">Registered Devices</h2>
            <button
              onClick={() => refetch()}
              className="bg-slate-700 hover:bg-slate-600 text-white px-3 py-1.5 rounded text-xs font-medium transition-colors"
            >
              Refresh
            </button>
          </div>
          {isLoading ? (
            <div className="p-8 text-center text-slate-400">Loading devices…</div>
          ) : devices.length === 0 ? (
            <div className="p-8 text-center text-slate-400">No devices registered.</div>
          ) : (
            <table className="w-full text-left">
              <thead className="bg-slate-700/50 text-slate-400 text-xs uppercase">
                <tr>
                  <th className="px-6 py-3 font-medium">Status</th>
                  <th className="px-6 py-3 font-medium">Hostname</th>
                  <th className="px-6 py-3 font-medium">OS</th>
                  <th className="px-6 py-3 font-medium">IP Address</th>
                  <th className="px-6 py-3 font-medium">Last Seen</th>
                  <th className="px-6 py-3 font-medium text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700">
                {devices.map((device) => (
                  <tr
                    key={device.id}
                    onClick={() => navigate(`/devices/${device.id}`)}
                    className={`hover:bg-slate-700/30 transition-colors group cursor-pointer ${
                      !device.isApproved ? 'bg-warning/5' : ''
                    }`}
                  >
                    <td className="px-6 py-4">
                      {device.isOnline ? (
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-success/10 text-success border border-success/20">
                          <span className="w-1.5 h-1.5 bg-success rounded-full mr-1.5"></span>
                          Online
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-700/50 text-slate-400 border border-slate-600">
                          <span className="w-1.5 h-1.5 bg-slate-500 rounded-full mr-1.5"></span>
                          Offline
                        </span>
                      )}
                      {!device.isApproved && (
                        <span className="ml-2 inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-warning/10 text-warning border border-warning/20">
                          <span className="w-1.5 h-1.5 bg-warning rounded-full mr-1.5 animate-pulse"></span>
                          Pending
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 font-medium text-white">{device.hostname}</td>
                    <td className="px-6 py-4 flex items-center gap-2">
                      {getPlatformIcon(device.platform)}
                      <span className="text-slate-300">{device.osVersion}</span>
                    </td>
                    <td className="px-6 py-4 text-slate-400 font-mono text-xs">{device.ipAddress ?? '—'}</td>
                    <td className="px-6 py-4 text-slate-400">{formatLastSeen(device.lastSeenAt)}</td>
                    <td className="px-6 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        {!device.isApproved ? (
                          <button
                            onClick={(e) => handleApprove(e, device)}
                            disabled={approveMutation.isPending}
                            className="bg-primary-600 hover:bg-primary-500 text-white px-3 py-1 rounded text-xs font-medium shadow-lg shadow-primary-500/20 disabled:opacity-50"
                          >
                            Approve
                          </button>
                        ) : (
                          <>
                            <button className="text-slate-400 hover:text-white px-2 py-1 rounded transition-colors text-xs">
                              Details
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  );
}
