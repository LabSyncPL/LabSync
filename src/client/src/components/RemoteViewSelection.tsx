import { useState, useMemo } from "react";
import type { DeviceDto } from "../types/device";
import { Grid, CheckSquare, Users, Server, Play, Search } from "./Icons";

interface RemoteViewSelectionProps {
  devices: DeviceDto[];
  onStartMonitoring: (selectedDeviceIds: string[]) => void;
}

type SelectionMode = "groups" | "custom";

export function RemoteViewSelection({
  devices,
  onStartMonitoring,
}: RemoteViewSelectionProps) {
  const [mode, setMode] = useState<SelectionMode>("groups");
  const [selectedGroups, setSelectedGroups] = useState<Set<string>>(new Set());
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<Set<string>>(
    new Set(),
  );
  const [searchTerm, setSearchTerm] = useState("");

  // Group devices by groupName
  const groups = useMemo(() => {
    const groupMap = new Map<
      string,
      { name: string; count: number; onlineCount: number; deviceIds: string[] }
    >();

    // Add "All Devices" group
    groupMap.set("all", {
      name: "All Devices",
      count: devices.length,
      onlineCount: devices.filter((d) => d.isOnline).length,
      deviceIds: devices.map((d) => d.id),
    });

    devices.forEach((d) => {
      const groupName = d.groupName || "Ungrouped";
      if (!groupMap.has(groupName)) {
        groupMap.set(groupName, {
          name: groupName,
          count: 0,
          onlineCount: 0,
          deviceIds: [],
        });
      }
      const group = groupMap.get(groupName)!;
      group.count++;
      if (d.isOnline) group.onlineCount++;
      group.deviceIds.push(d.id);
    });

    return Array.from(groupMap.values());
  }, [devices]);

  const toggleGroup = (groupName: string, deviceIds: string[]) => {
    const newSelectedGroups = new Set(selectedGroups);
    const newSelectedDeviceIds = new Set(selectedDeviceIds);

    if (newSelectedGroups.has(groupName)) {
      newSelectedGroups.delete(groupName);
      deviceIds.forEach((id) => newSelectedDeviceIds.delete(id));
    } else {
      newSelectedGroups.add(groupName);
      deviceIds.forEach((id) => newSelectedDeviceIds.add(id));
    }

    setSelectedGroups(newSelectedGroups);
    setSelectedDeviceIds(newSelectedDeviceIds);
  };

  const toggleDevice = (deviceId: string) => {
    const newSelectedDeviceIds = new Set(selectedDeviceIds);
    if (newSelectedDeviceIds.has(deviceId)) {
      newSelectedDeviceIds.delete(deviceId);
    } else {
      newSelectedDeviceIds.add(deviceId);
    }
    setSelectedDeviceIds(newSelectedDeviceIds);
    // Clear group selection if we're modifying individual devices to avoid confusion
    setSelectedGroups(new Set());
  };

  const filteredDevices = useMemo(() => {
    return devices.filter(
      (d) =>
        d.hostname.toLowerCase().includes(searchTerm.toLowerCase()) ||
        d.ipAddress?.includes(searchTerm),
    );
  }, [devices, searchTerm]);

  const handleStart = () => {
    onStartMonitoring(Array.from(selectedDeviceIds));
  };

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-900 text-slate-700 dark:text-slate-200 p-8 flex flex-col items-center scrollbar-light dark:scrollbar-dark">
      <div className="w-full max-w-5xl space-y-8">
        {/* Header */}
        <div className="text-center space-y-2">
          <h1 className="text-3xl font-bold text-slate-900 dark:text-white flex items-center justify-center gap-3">
            Remote View Selection
          </h1>
          <p className="text-slate-500 dark:text-slate-400">
            Select the devices you want to monitor in the video wall.
          </p>
        </div>

        {/* Mode Toggle */}
        <div className="flex justify-center">
          <div className="bg-white dark:bg-slate-800 p-1 rounded-lg inline-flex border border-slate-200 dark:border-slate-700 shadow-sm">
            <button
              onClick={() => setMode("groups")}
              className={`px-6 py-2 rounded-md text-sm font-medium transition-all flex items-center gap-2 ${
                mode === "groups"
                  ? "bg-primary-600 text-white shadow-lg shadow-primary-500/20"
                  : "text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-50 dark:hover:bg-slate-700"
              }`}
            >
              <Grid className="w-4 h-4" />
              Groups / Rooms
            </button>
            <button
              onClick={() => setMode("custom")}
              className={`px-6 py-2 rounded-md text-sm font-medium transition-all flex items-center gap-2 ${
                mode === "custom"
                  ? "bg-primary-600 text-white shadow-lg shadow-primary-500/20"
                  : "text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-50 dark:hover:bg-slate-700"
              }`}
            >
              <CheckSquare className="w-4 h-4" />
              Custom Selection
            </button>
          </div>
        </div>

        {/* Content Area */}
        <div className="bg-white dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 rounded-2xl p-6 min-h-[400px] shadow-sm">
          {mode === "groups" ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {groups.map((group) => (
                <div
                  key={group.name}
                  onClick={() => toggleGroup(group.name, group.deviceIds)}
                  className={`relative p-5 rounded-xl border-2 cursor-pointer transition-all hover:scale-[1.02] shadow-sm ${
                    selectedGroups.has(group.name)
                      ? "border-primary-500 bg-primary-50 dark:bg-primary-900/20"
                      : "border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 hover:border-slate-300 dark:hover:border-slate-600"
                  }`}
                >
                  <div className="flex justify-between items-start mb-4">
                    <div
                      className={`p-3 rounded-lg ${
                        selectedGroups.has(group.name)
                          ? "bg-primary-100 dark:bg-primary-500/20 text-primary-600 dark:text-primary-400"
                          : "bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400"
                      }`}
                    >
                      {group.name === "All Devices" ? (
                        <Server className="w-6 h-6" />
                      ) : (
                        <Users className="w-6 h-6" />
                      )}
                    </div>
                    {selectedGroups.has(group.name) && (
                      <div className="bg-primary-500 text-white p-1 rounded-full shadow-lg shadow-primary-500/20">
                        <CheckSquare className="w-4 h-4" />
                      </div>
                    )}
                  </div>

                  <h3 className="text-lg font-bold text-slate-900 dark:text-white mb-1">
                    {group.name}
                  </h3>
                  <div className="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
                    <span
                      className={
                        group.onlineCount > 0
                          ? "text-success"
                          : "text-slate-400 dark:text-slate-500"
                      }
                    >
                      {group.onlineCount} online
                    </span>
                    <span>/</span>
                    <span>{group.count} total</span>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="space-y-4">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400 dark:text-slate-500" />
                <input
                  type="text"
                  placeholder="Search devices..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="w-full bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg py-2 pl-10 pr-4 text-sm text-slate-900 dark:text-white focus:outline-none focus:border-primary-500 transition-colors"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3 max-h-[500px] overflow-y-auto pr-2 scrollbar-light dark:scrollbar-dark">
                {filteredDevices.map((d) => (
                  <div
                    key={d.id}
                    onClick={() => toggleDevice(d.id)}
                    className={`flex items-center gap-3 p-3 rounded-lg border-2 cursor-pointer transition-all ${
                      selectedDeviceIds.has(d.id)
                        ? "border-primary-500 bg-primary-50 dark:bg-primary-900/20"
                        : "border-slate-100 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-800 hover:border-slate-200 dark:hover:border-slate-600"
                    }`}
                  >
                    <div
                      className={`w-4 h-4 rounded border-2 flex items-center justify-center transition-colors ${
                        selectedDeviceIds.has(d.id)
                          ? "bg-primary-500 border-primary-500"
                          : "border-slate-300 dark:border-slate-600"
                      }`}
                    >
                      {selectedDeviceIds.has(d.id) && (
                        <CheckSquare className="w-3 h-3 text-white" />
                      )}
                    </div>
                    <div className="flex-1 truncate">
                      <div className="text-sm font-bold text-slate-900 dark:text-white truncate">
                        {d.hostname}
                      </div>
                      <div className="text-[10px] text-slate-500 dark:text-slate-400">
                        {d.ipAddress || "No IP"}
                      </div>
                    </div>
                    <div
                      className={`w-2 h-2 rounded-full ${
                        d.isOnline ? "bg-success shadow-[0_0_8px_rgba(34,197,94,0.4)]" : "bg-slate-300 dark:bg-slate-600"
                      }`}
                    />
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="flex justify-between items-center bg-white dark:bg-slate-800 p-6 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg">
          <div className="space-y-1">
            <div className="text-xl font-bold text-slate-900 dark:text-white">
              {selectedDeviceIds.size} Devices Selected
            </div>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Ready to start real-time monitoring wall.
            </p>
          </div>
          <button
            onClick={handleStart}
            disabled={selectedDeviceIds.size === 0}
            className="px-8 py-3 bg-primary-600 hover:bg-primary-500 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-xl font-bold flex items-center gap-3 transition-all transform hover:scale-105 active:scale-95 shadow-xl shadow-primary-500/20"
          >
            <Play className="w-5 h-5 fill-current" />
            Start Monitoring
          </button>
        </div>
      </div>
    </div>
  );
}
