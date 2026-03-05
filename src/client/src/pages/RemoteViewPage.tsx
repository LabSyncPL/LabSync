import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchDevices, approveDevice, devicesQueryKey } from "../api/devices";
import type { DeviceDto } from "../types/device";
import { useState, useMemo, useEffect } from "react";
import {
  DeviceFilterControls,
  type DeviceFilters,
} from "../components/Dashboard/DeviceFilterControls";
import { DeviceGridCard } from "../components/Dashboard/DeviceGridCard";
import { useGridMonitor } from "../hooks/useGridMonitor";
import { RemoteControlModal } from "../components/RemoteControl/RemoteControlModal";

export function RemoteViewPage() {
  const queryClient = useQueryClient();

  // Grid Monitor Hook
  const {
    subscribe,
    unsubscribe,
    images,
    isConnected: isMonitorConnected,
  } = useGridMonitor();
  const [isMonitoring, setIsMonitoring] = useState(false);
  const [remoteControlDeviceId, setRemoteControlDeviceId] = useState<
    string | null
  >(null);

  const [filters, setFilters] = useState<DeviceFilters>({
    search: "",
    status: "all",
    platform: "all",
    group: "all",
    viewMode: "grid", // Always grid for Remote View
  });

  const {
    data: devices = [],
    isLoading,
    isError,
    error,
    isFetching,
    refetch,
  } = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
    refetchInterval: 30000,
  });

  const [approvingId, setApprovingId] = useState<string | null>(null);

  const approveMutation = useMutation({
    mutationFn: approveDevice,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
    },
  });

  const handleApprove = async (e: React.MouseEvent, device: DeviceDto) => {
    e.stopPropagation();
    if (device.isApproved) return;
    try {
      setApprovingId(device.id);
      await approveMutation.mutateAsync(device.id);
    } catch {
      alert("Failed to approve device.");
    } finally {
      setApprovingId((prev) => (prev === device.id ? null : prev));
    }
  };

  const uniqueGroups = useMemo(() => {
    const groups = new Set<string>();
    devices.forEach((d) => {
      if (d.groupName) groups.add(d.groupName);
    });
    return Array.from(groups).sort();
  }, [devices]);

  const filteredDevices = useMemo(() => {
    return devices.filter((device) => {
      const matchesSearch =
        device.hostname.toLowerCase().includes(filters.search.toLowerCase()) ||
        device.ipAddress?.includes(filters.search) ||
        device.osVersion.toLowerCase().includes(filters.search.toLowerCase());

      const matchesStatus =
        filters.status === "all" ||
        (filters.status === "online" && device.isOnline) ||
        (filters.status === "offline" && !device.isOnline) ||
        (filters.status === "pending" && !device.isApproved);

      const matchesPlatform =
        filters.platform === "all" || device.platform === filters.platform;

      const matchesGroup =
        filters.group === "all" ||
        (filters.group === "no-group" && !device.groupName) ||
        device.groupName === filters.group;

      return matchesSearch && matchesStatus && matchesPlatform && matchesGroup;
    });
  }, [devices, filters]);

  // Monitor Subscription Logic
  useEffect(() => {
    if (!isMonitorConnected) return;

    if (isMonitoring && filteredDevices.length > 0) {
      // Subscribe to all currently filtered devices
      const deviceIds = filteredDevices.map((d) => d.id);
      subscribe(deviceIds);

      return () => {
        unsubscribe(deviceIds);
      };
    }
  }, [
    isMonitoring,
    filteredDevices,
    isMonitorConnected,
    subscribe,
    unsubscribe,
  ]);

  // Auto-start monitoring when entering Remote View if connected
  useEffect(() => {
    if (isMonitorConnected && !isMonitoring) {
      setIsMonitoring(true);
    }
  }, [isMonitorConnected]);

  return (
    <div className="flex flex-col h-full relative">
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <div className="flex items-center gap-8">
          <h1 className="text-xl font-semibold text-white">Remote View</h1>

          <div className="hidden lg:flex items-center gap-6 border-l border-slate-800 pl-6 h-8">
            <div className="flex items-center gap-2 text-sm">
              <span className="text-slate-400">Monitoring</span>
              <span
                className={`font-semibold px-2 py-0.5 rounded border ${isMonitoring ? "text-success bg-success/10 border-success/20" : "text-slate-400 bg-slate-800 border-slate-700"}`}
              >
                {isMonitoring ? "Active" : "Paused"}
              </span>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <span className="text-slate-400">Devices</span>
              <span className="text-white font-semibold bg-slate-800 px-2 py-0.5 rounded border border-slate-700">
                {filteredDevices.length}
              </span>
            </div>
          </div>
        </div>

        <div className="flex items-center space-x-4">
          <button
            type="button"
            onClick={() => setIsMonitoring(!isMonitoring)}
            disabled={!isMonitorConnected}
            className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-colors border flex items-center gap-2 ${
              isMonitoring
                ? "bg-danger/10 border-danger/20 text-danger hover:bg-danger/20"
                : "bg-success/10 border-success/20 text-success hover:bg-success/20"
            } ${!isMonitorConnected ? "opacity-50 cursor-not-allowed" : ""}`}
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              {isMonitoring ? (
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z M15 9l-6 6M9 9l6 6"
                />
              ) : (
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M15 12a3 3 0 11-6 0 3 3 0 016 0z M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"
                />
              )}
            </svg>
            {isMonitoring ? "Stop Monitor" : "Live Monitor"}
          </button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto scrollbar-thin scrollbar-thumb-slate-700 scrollbar-track-slate-900 p-8">
        <div className="max-w-[1600px] mx-auto space-y-6">
          <DeviceFilterControls
            filters={filters}
            groups={uniqueGroups}
            onFilterChange={(newFilters) =>
              setFilters({ ...newFilters, viewMode: "grid" })
            } // Enforce grid
            onRefresh={() => refetch()}
            isRefreshing={isFetching}
          />

          {isLoading ? (
            <div className="flex flex-col items-center justify-center py-20 text-slate-500">
              <p>Loading devices...</p>
            </div>
          ) : isError ? (
            <div className="bg-danger/10 border border-danger/20 rounded-2xl p-8 text-center max-w-lg mx-auto mt-12">
              <p className="text-slate-400 mb-6 text-sm">
                {error instanceof Error
                  ? error.message
                  : "An unknown error occurred while fetching device data."}
              </p>
              <button
                onClick={() => refetch()}
                className="bg-danger hover:bg-danger/90 text-white px-6 py-2 rounded-lg text-sm font-medium transition-colors"
              >
                Try Again
              </button>
            </div>
          ) : filteredDevices.length === 0 ? (
            <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl p-16 text-center">
              <h3 className="text-white font-medium mb-1">No devices found</h3>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
              {filteredDevices.map((device) => (
                <DeviceGridCard
                  key={device.id}
                  device={device}
                  onApprove={handleApprove}
                  isApproving={approvingId === device.id}
                  monitorImageSrc={isMonitoring ? images[device.id] : undefined}
                  onDoubleClick={(d) => setRemoteControlDeviceId(d.id)}
                />
              ))}
            </div>
          )}
        </div>
      </div>

      {remoteControlDeviceId && (
        <RemoteControlModal
          deviceId={remoteControlDeviceId}
          onClose={() => setRemoteControlDeviceId(null)}
        />
      )}
    </div>
  );
}
