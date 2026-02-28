import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchDevices, approveDevice, devicesQueryKey } from "../api/devices";
import { clearToken } from "../auth/authStore";
import { useNavigate } from "react-router-dom";
import type { DeviceDto } from "../types/device";
import { useState, useMemo } from "react";
import {
  DeviceFilterControls,
  type DeviceFilters,
} from "../components/Dashboard/DeviceFilterControls";
import { DeviceGridCard } from "../components/Dashboard/DeviceGridCard";
import { DeviceListItem } from "../components/Dashboard/DeviceListItem";

export function Dashboard() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [filters, setFilters] = useState<DeviceFilters>({
    search: "",
    status: "all",
    platform: "all",
    group: "all",
    viewMode: "grid",
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

  const handleLogout = () => {
    clearToken();
    navigate("/login");
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

  const totalDevices = devices.length;
  const onlineDevices = devices.filter((d) => d.isOnline).length;
  const pendingDevices = devices.filter((d) => !d.isApproved).length;

  return (
    <div className="flex flex-col h-full">
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <div className="flex items-center gap-8">
          <h1 className="text-xl font-semibold text-white">Overview</h1>

          <div className="hidden lg:flex items-center gap-6 border-l border-slate-800 pl-6 h-8">
            <div className="flex items-center gap-2 text-sm">
              <span className="text-slate-400">Devices</span>
              <span className="text-white font-semibold bg-slate-800 px-2 py-0.5 rounded border border-slate-700">
                {totalDevices}
              </span>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <span className="text-slate-400">Online</span>
              <span className="text-success font-semibold bg-success/10 px-2 py-0.5 rounded border border-success/20">
                {onlineDevices}
              </span>
            </div>
            {pendingDevices > 0 && (
              <div className="flex items-center gap-2 text-sm animate-pulse">
                <span className="text-warning">Pending</span>
                <span className="text-warning font-semibold bg-warning/10 px-2 py-0.5 rounded border border-warning/20">
                  {pendingDevices}
                </span>
              </div>
            )}
          </div>
        </div>

        <div className="flex items-center space-x-4">
          <button
            type="button"
            className="text-primary-400 hover:text-primary-300 px-3 py-1.5 rounded-lg text-sm font-medium transition-colors border border-primary-500/20 bg-primary-500/5 hover:bg-primary-500/10 flex items-center gap-2"
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M12 4v16m8-8H4"
              />
            </svg>
            Create Group
          </button>
          <div className="h-6 w-px bg-slate-800"></div>
          <button
            type="button"
            onClick={handleLogout}
            className="text-slate-400 hover:text-white px-3 py-1.5 rounded-lg text-sm transition-colors"
          >
            Log out
          </button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto scrollbar-thin scrollbar-thumb-slate-700 scrollbar-track-slate-900 p-8">
        <div className="max-w-[1600px] mx-auto space-y-6">
          <DeviceFilterControls
            filters={filters}
            groups={uniqueGroups}
            onFilterChange={setFilters}
            onRefresh={() => refetch()}
            isRefreshing={isFetching}
          />

          {isLoading ? (
            <div className="flex flex-col items-center justify-center py-20 text-slate-500">
              <svg
                className="w-10 h-10 animate-spin mb-4 text-primary-500/50"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                />
              </svg>
              <p>Loading devices...</p>
            </div>
          ) : isError ? (
            <div className="bg-danger/10 border border-danger/20 rounded-2xl p-8 text-center max-w-lg mx-auto mt-12">
              <svg
                className="w-12 h-12 text-danger mx-auto mb-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                />
              </svg>
              <h3 className="text-lg font-bold text-white mb-2">
                Failed to load devices
              </h3>
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
              <div className="w-16 h-16 bg-slate-800 rounded-full flex items-center justify-center mx-auto mb-4 border border-slate-700">
                <svg
                  className="w-8 h-8 text-slate-500"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="2"
                    d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                  />
                </svg>
              </div>
              <h3 className="text-white font-medium mb-1">No devices found</h3>
              <p className="text-slate-500 text-sm">
                {devices.length === 0
                  ? "No devices have registered with the server yet."
                  : "Try adjusting your filters to see more results."}
              </p>
              {devices.length > 0 && (
                <button
                  onClick={() =>
                    setFilters({
                      search: "",
                      status: "all",
                      platform: "all",
                      group: "all",
                      viewMode: filters.viewMode,
                    })
                  }
                  className="mt-4 text-primary-400 hover:text-primary-300 text-sm font-medium"
                >
                  Clear all filters
                </button>
              )}
            </div>
          ) : (
            <div
              className={
                filters.viewMode === "grid"
                  ? "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6"
                  : "flex flex-col gap-3"
              }
            >
              {filteredDevices.map((device) =>
                filters.viewMode === "grid" ? (
                  <DeviceGridCard
                    key={device.id}
                    device={device}
                    onApprove={handleApprove}
                    isApproving={approvingId === device.id}
                  />
                ) : (
                  <DeviceListItem
                    key={device.id}
                    device={device}
                    onApprove={handleApprove}
                    isApproving={approvingId === device.id}
                  />
                ),
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
