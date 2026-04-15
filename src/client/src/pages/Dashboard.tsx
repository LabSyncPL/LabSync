import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  assignDeviceToGroup,
  approveDevice,
  deleteDevice,
  devicesQueryKey,
  fetchDevices,
  removeDeviceFromGroup,
} from "../api/devices";
import {
  createDeviceGroup,
  deleteDeviceGroup,
  deviceGroupsQueryKey,
  fetchDeviceGroups,
  updateDeviceGroup,
} from "../api/deviceGroups";
import { clearToken } from "../auth/authStore";
import { useNavigate } from "react-router-dom";
import type { DeviceDto } from "../types/device";
import { useState, useMemo } from "react";
import type { DeviceGroupDto } from "../types/deviceGroups";
import {
  DeviceFilterControls,
  type DeviceFilters,
} from "../components/Dashboard/DeviceFilterControls";
import { DeviceGridCard } from "../components/Dashboard/DeviceGridCard";
import { DeviceListItem } from "../components/Dashboard/DeviceListItem";
import { extractApiErrorMessage } from "../api/scriptRunner";
import { createCollectMetricsJob } from "../api/jobs";

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
  const [groupsModalOpen, setGroupsModalOpen] = useState(false);
  const [groupFormName, setGroupFormName] = useState("");
  const [groupFormDescription, setGroupFormDescription] = useState("");
  const [editingGroupId, setEditingGroupId] = useState<string | null>(null);
  const [activeGroupId, setActiveGroupId] = useState<string | null>(null);
  const [assigningDevice, setAssigningDevice] = useState<DeviceDto | null>(null);
  const [assignTargetGroupId, setAssignTargetGroupId] = useState<string>("");
  const [groupsError, setGroupsError] = useState<string | null>(null);
  const [deviceActionError, setDeviceActionError] = useState<string | null>(null);

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
  const groupsQuery = useQuery({
    queryKey: deviceGroupsQueryKey,
    queryFn: fetchDeviceGroups,
  });

  const [approvingId, setApprovingId] = useState<string | null>(null);

  const approveMutation = useMutation({
    mutationFn: approveDevice,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
    },
  });
  const createGroupMutation = useMutation({
    mutationFn: createDeviceGroup,
    onSuccess: () => {
      setGroupsError(null);
      setGroupFormName("");
      setGroupFormDescription("");
      queryClient.invalidateQueries({ queryKey: deviceGroupsQueryKey });
    },
    onError: (error) => setGroupsError(extractApiErrorMessage(error)),
  });
  const updateGroupMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: { name: string; description?: string | null } }) =>
      updateDeviceGroup(id, payload),
    onSuccess: () => {
      setGroupsError(null);
      setEditingGroupId(null);
      setGroupFormName("");
      setGroupFormDescription("");
      queryClient.invalidateQueries({ queryKey: deviceGroupsQueryKey });
    },
    onError: (error) => setGroupsError(extractApiErrorMessage(error)),
  });
  const deleteGroupMutation = useMutation({
    mutationFn: deleteDeviceGroup,
    onSuccess: (_, deletedId) => {
      setGroupsError(null);
      if (activeGroupId === deletedId) setActiveGroupId(null);
      queryClient.invalidateQueries({ queryKey: deviceGroupsQueryKey });
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
    },
    onError: (error) => setGroupsError(extractApiErrorMessage(error)),
  });
  const assignDeviceMutation = useMutation({
    mutationFn: ({ deviceId, groupId }: { deviceId: string; groupId: string }) =>
      assignDeviceToGroup(deviceId, groupId),
    onSuccess: () => {
      setGroupsError(null);
      setAssigningDevice(null);
      setAssignTargetGroupId("");
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
      queryClient.invalidateQueries({ queryKey: deviceGroupsQueryKey });
    },
    onError: (error) => setGroupsError(extractApiErrorMessage(error)),
  });
  const removeDeviceMutation = useMutation({
    mutationFn: removeDeviceFromGroup,
    onSuccess: () => {
      setGroupsError(null);
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
      queryClient.invalidateQueries({ queryKey: deviceGroupsQueryKey });
    },
    onError: (error) => setGroupsError(extractApiErrorMessage(error)),
  });
  const deleteDeviceMutation = useMutation({
    mutationFn: deleteDevice,
    onSuccess: () => {
      setDeviceActionError(null);
      queryClient.invalidateQueries({ queryKey: devicesQueryKey });
      queryClient.invalidateQueries({ queryKey: deviceGroupsQueryKey });
    },
    onError: (error) => setDeviceActionError(extractApiErrorMessage(error)),
  });
  const diagnosticsMutation = useMutation({
    mutationFn: createCollectMetricsJob,
    onSuccess: () => {
      setDeviceActionError(null);
    },
    onError: (error) => setDeviceActionError(extractApiErrorMessage(error)),
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

  const groups = groupsQuery.data ?? [];
  const uniqueGroups = useMemo(() => groups.map((group) => group.name), [groups]);
  const activeGroup = useMemo(
    () => groups.find((group) => group.id === activeGroupId) ?? null,
    [groups, activeGroupId],
  );

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
        filters.platform === "all" || device.platform === Number(filters.platform);

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
  const upsertGroup = async () => {
    const name = groupFormName.trim();
    if (!name) {
      setGroupsError("Group name is required.");
      return;
    }

    const payload = {
      name,
      description: groupFormDescription.trim() || null,
    };

    if (editingGroupId) {
      await updateGroupMutation.mutateAsync({ id: editingGroupId, payload });
      return;
    }

    await createGroupMutation.mutateAsync(payload);
  };

  const beginEditGroup = (group: DeviceGroupDto) => {
    setEditingGroupId(group.id);
    setGroupFormName(group.name);
    setGroupFormDescription(group.description || "");
  };

  const handleAssignFromMenu = (device: DeviceDto) => {
    setAssigningDevice(device);
    setAssignTargetGroupId(device.groupId || "");
  };

  const handleRemoveFromMenu = async (device: DeviceDto) => {
    if (!device.groupId) return;
    await removeDeviceMutation.mutateAsync(device.id);
  };

  const handleViewDetails = (device: DeviceDto) => {
    navigate(`/devices/${device.id}`);
  };

  const handleCopyDeviceId = async (device: DeviceDto) => {
    try {
      await navigator.clipboard.writeText(device.id);
      setDeviceActionError(null);
    } catch {
      setDeviceActionError("Failed to copy device ID.");
    }
  };

  const handleRunQuickDiagnostics = async (device: DeviceDto) => {
    if (!device.isApproved || !device.isOnline) {
      setDeviceActionError("Quick diagnostics is available only for approved online devices.");
      return;
    }
    await diagnosticsMutation.mutateAsync(device.id);
  };

  const handleDeleteDevice = async (device: DeviceDto) => {
    const confirmed = window.confirm(
      `Delete device "${device.hostname}"? This action is irreversible and removes related records.`,
    );
    if (!confirmed) return;
    await deleteDeviceMutation.mutateAsync(device.id);
  };

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
            onClick={() => setGroupsModalOpen(true)}
            className="text-slate-300 hover:text-white px-3 py-1.5 rounded-lg text-sm transition-colors border border-slate-700 bg-slate-800/60 hover:bg-slate-700/70"
          >
            Groups
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
          {deviceActionError && (
            <p className="text-xs text-rose-300 -mt-2">{deviceActionError}</p>
          )}

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
                    onAssignToGroup={handleAssignFromMenu}
                    onRemoveFromGroup={handleRemoveFromMenu}
                    onViewDetails={handleViewDetails}
                    onCopyDeviceId={handleCopyDeviceId}
                    onRunQuickDiagnostics={handleRunQuickDiagnostics}
                    onDeleteDevice={handleDeleteDevice}
                  />
                ) : (
                  <DeviceListItem
                    key={device.id}
                    device={device}
                    onApprove={handleApprove}
                    isApproving={approvingId === device.id}
                    onAssignToGroup={handleAssignFromMenu}
                    onRemoveFromGroup={handleRemoveFromMenu}
                    onViewDetails={handleViewDetails}
                    onCopyDeviceId={handleCopyDeviceId}
                    onRunQuickDiagnostics={handleRunQuickDiagnostics}
                    onDeleteDevice={handleDeleteDevice}
                  />
                ),
              )}
            </div>
          )}
        </div>
      </div>

      {groupsModalOpen && (
        <div
          className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4"
          onClick={() => setGroupsModalOpen(false)}
        >
          <div
            className="w-full max-w-5xl bg-slate-900 border border-slate-700 rounded-2xl shadow-xl p-4 md:p-5 space-y-4"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between gap-2">
              <div>
                <h2 className="text-white text-sm font-semibold">Device Groups</h2>
                <p className="text-xs text-slate-400">{groups.length} groups</p>
              </div>
              <button
                type="button"
                onClick={() => setGroupsModalOpen(false)}
                className="text-xs text-slate-300 hover:text-white bg-slate-800 border border-slate-700 rounded px-2 py-1"
              >
                Close
              </button>
            </div>
            <div className="grid gap-4 lg:grid-cols-[320px_1fr]">
              <div className="border border-slate-700 rounded-xl p-3 space-y-2 bg-slate-900/60">
                <p className="text-xs text-slate-400">
                  {editingGroupId ? "Edit group" : "Create group"}
                </p>
                <input
                  value={groupFormName}
                  onChange={(e) => setGroupFormName(e.target.value)}
                  placeholder="Group name"
                  className="w-full bg-slate-900 border border-slate-700 text-white text-sm rounded-lg px-3 py-2"
                />
                <textarea
                  value={groupFormDescription}
                  onChange={(e) => setGroupFormDescription(e.target.value)}
                  placeholder="Description (optional)"
                  rows={3}
                  className="w-full bg-slate-900 border border-slate-700 text-white text-sm rounded-lg px-3 py-2 resize-none"
                />
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={upsertGroup}
                    disabled={createGroupMutation.isPending || updateGroupMutation.isPending}
                    className="bg-primary-600 hover:bg-primary-500 disabled:opacity-50 text-white text-xs px-3 py-2 rounded-lg"
                  >
                    {editingGroupId ? "Update Group" : "Create Group"}
                  </button>
                  {editingGroupId && (
                    <button
                      type="button"
                      onClick={() => {
                        setEditingGroupId(null);
                        setGroupFormName("");
                        setGroupFormDescription("");
                      }}
                      className="bg-slate-700 hover:bg-slate-600 text-white text-xs px-3 py-2 rounded-lg"
                    >
                      Cancel
                    </button>
                  )}
                </div>
              </div>
              <div className="space-y-3">
                <div className="border border-slate-700 rounded-xl divide-y divide-slate-700 bg-slate-900/40 max-h-56 overflow-auto">
                  {groupsQuery.isLoading ? (
                    <p className="p-3 text-sm text-slate-400">Loading groups…</p>
                  ) : groups.length === 0 ? (
                    <p className="p-3 text-sm text-slate-400">No groups created yet.</p>
                  ) : (
                    groups.map((group) => (
                      <div
                        key={group.id}
                        className={`p-3 flex items-start justify-between gap-3 ${
                          activeGroupId === group.id ? "bg-primary-500/10" : ""
                        }`}
                      >
                        <button
                          type="button"
                          onClick={() => setActiveGroupId(group.id)}
                          className="text-left flex-1"
                        >
                          <p className="text-sm text-white font-medium">{group.name}</p>
                          {group.description && (
                            <p className="text-xs text-slate-400 mt-0.5">{group.description}</p>
                          )}
                          <p className="text-[11px] text-slate-500 mt-1">
                            {group.deviceCount} device(s)
                          </p>
                        </button>
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() => beginEditGroup(group)}
                            className="text-xs text-slate-300 hover:text-white"
                          >
                            Edit
                          </button>
                          <button
                            type="button"
                            onClick={() => {
                              if (window.confirm(`Delete group "${group.name}"?`)) {
                                deleteGroupMutation.mutate(group.id);
                              }
                            }}
                            className="text-xs text-rose-300 hover:text-rose-200"
                          >
                            Delete
                          </button>
                        </div>
                      </div>
                    ))
                  )}
                </div>
                <div className="border border-slate-700 rounded-xl p-3 bg-slate-900/40">
                  <p className="text-xs uppercase tracking-wide text-slate-500 mb-2">
                    Group devices
                  </p>
                  {activeGroup ? (
                    activeGroup.devices.length > 0 ? (
                      <ul className="space-y-1 max-h-28 overflow-auto pr-1">
                        {activeGroup.devices.map((device) => (
                          <li key={device.id} className="text-xs text-slate-300 flex justify-between">
                            <span>{device.hostname}</span>
                            <span className={device.isOnline ? "text-success" : "text-slate-500"}>
                              {device.isOnline ? "Online" : "Offline"}
                            </span>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="text-xs text-slate-400">No devices in selected group.</p>
                    )
                  ) : (
                    <p className="text-xs text-slate-400">Select a group to view its devices.</p>
                  )}
                </div>
              </div>
            </div>
            {groupsError && <p className="text-xs text-rose-300">{groupsError}</p>}
          </div>
        </div>
      )}

      {assigningDevice && (
        <div
          className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4"
          onClick={() => setAssigningDevice(null)}
        >
          <div
            className="w-full max-w-sm bg-slate-900 border border-slate-700 rounded-xl shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="px-4 py-3 border-b border-slate-800">
              <h3 className="text-sm font-semibold text-white">Assign to group</h3>
              <p className="text-xs text-slate-400 mt-1">{assigningDevice.hostname}</p>
            </div>
            <div className="p-4 space-y-3">
              <select
                value={assignTargetGroupId}
                onChange={(e) => setAssignTargetGroupId(e.target.value)}
                className="w-full bg-slate-800 border border-slate-700 text-white text-sm rounded-lg px-3 py-2"
              >
                <option value="">Select a group</option>
                {groups.map((group) => (
                  <option key={group.id} value={group.id}>
                    {group.name}
                  </option>
                ))}
              </select>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setAssigningDevice(null)}
                  className="bg-slate-800 border border-slate-700 hover:bg-slate-700 text-white text-xs px-3 py-2 rounded-lg"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={async () => {
                    if (!assignTargetGroupId) {
                      setGroupsError("Select a target group.");
                      return;
                    }
                    await assignDeviceMutation.mutateAsync({
                      deviceId: assigningDevice.id,
                      groupId: assignTargetGroupId,
                    });
                  }}
                  disabled={assignDeviceMutation.isPending}
                  className="bg-primary-600 hover:bg-primary-500 disabled:opacity-50 text-white text-xs px-3 py-2 rounded-lg"
                >
                  Assign
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
