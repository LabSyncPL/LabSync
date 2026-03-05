import { useState, useMemo } from "react";
import type { DeviceDto } from "../types/device";
import {
  Monitor,
  Grid,
  CheckSquare,
  Users,
  Server,
  Play,
  Search,
} from "./Icons";

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
    <div className="min-h-screen bg-slate-900 text-slate-200 p-8 flex flex-col items-center">
      <div className="w-full max-w-5xl space-y-8">
        {/* Header */}
        <div className="text-center space-y-2">
          <h1 className="text-3xl font-bold text-white flex items-center justify-center gap-3">
            <Monitor className="w-8 h-8 text-primary-400" />
            Remote View Selection
          </h1>
          <p className="text-slate-400">
            Select the devices you want to monitor in the video wall.
          </p>
        </div>

        {/* Mode Toggle */}
        <div className="flex justify-center">
          <div className="bg-slate-800 p-1 rounded-lg inline-flex border border-slate-700">
            <button
              onClick={() => setMode("groups")}
              className={`px-6 py-2 rounded-md text-sm font-medium transition-all flex items-center gap-2 ${
                mode === "groups"
                  ? "bg-primary-600 text-white shadow-lg"
                  : "text-slate-400 hover:text-white hover:bg-slate-700"
              }`}
            >
              <Grid className="w-4 h-4" />
              Groups / Rooms
            </button>
            <button
              onClick={() => setMode("custom")}
              className={`px-6 py-2 rounded-md text-sm font-medium transition-all flex items-center gap-2 ${
                mode === "custom"
                  ? "bg-primary-600 text-white shadow-lg"
                  : "text-slate-400 hover:text-white hover:bg-slate-700"
              }`}
            >
              <CheckSquare className="w-4 h-4" />
              Custom Selection
            </button>
          </div>
        </div>

        {/* Content Area */}
        <div className="bg-slate-800/50 border border-slate-700 rounded-2xl p-6 min-h-[400px]">
          {mode === "groups" ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {groups.map((group) => (
                <div
                  key={group.name}
                  onClick={() => toggleGroup(group.name, group.deviceIds)}
                  className={`relative p-5 rounded-xl border-2 cursor-pointer transition-all hover:scale-[1.02] ${
                    selectedGroups.has(group.name)
                      ? "border-primary-500 bg-primary-900/20"
                      : "border-slate-700 bg-slate-800 hover:border-slate-600"
                  }`}
                >
                  <div className="flex justify-between items-start mb-4">
                    <div
                      className={`p-3 rounded-lg ${
                        selectedGroups.has(group.name)
                          ? "bg-primary-500/20 text-primary-400"
                          : "bg-slate-700 text-slate-400"
                      }`}
                    >
                      {group.name === "All Devices" ? (
                        <Server className="w-6 h-6" />
                      ) : (
                        <Users className="w-6 h-6" />
                      )}
                    </div>
                    {selectedGroups.has(group.name) && (
                      <div className="bg-primary-500 text-white p-1 rounded-full">
                        <CheckSquare className="w-4 h-4" />
                      </div>
                    )}
                  </div>

                  <h3 className="text-lg font-bold text-white mb-1">
                    {group.name}
                  </h3>
                  <div className="flex items-center gap-2 text-sm text-slate-400">
                    <span
                      className={
                        group.onlineCount > 0
                          ? "text-success"
                          : "text-slate-500"
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
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input
                  type="text"
                  placeholder="Search devices..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="w-full bg-slate-900 border border-slate-700 rounded-lg pl-10 pr-4 py-2 text-white focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3 max-h-[500px] overflow-y-auto pr-2 custom-scrollbar">
                {filteredDevices.map((device) => (
                  <div
                    key={device.id}
                    onClick={() => toggleDevice(device.id)}
                    className={`flex items-center gap-3 p-3 rounded-lg border cursor-pointer transition-colors ${
                      selectedDeviceIds.has(device.id)
                        ? "border-primary-500 bg-primary-900/20"
                        : "border-slate-700 bg-slate-800 hover:bg-slate-700"
                    }`}
                  >
                    <div
                      className={`w-3 h-3 rounded-full ${device.isOnline ? "bg-success shadow-[0_0_8px_rgba(34,197,94,0.4)]" : "bg-slate-600"}`}
                    />
                    <div className="flex-1 min-w-0">
                      <div className="font-medium text-white truncate">
                        {device.hostname}
                      </div>
                      <div className="text-xs text-slate-400 truncate">
                        {device.ipAddress}
                      </div>
                    </div>
                    {selectedDeviceIds.has(device.id) && (
                      <CheckSquare className="w-4 h-4 text-primary-400 shrink-0" />
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Footer Action */}
        <div className="flex justify-end pt-4 border-t border-slate-800">
          <button
            onClick={handleStart}
            disabled={selectedDeviceIds.size === 0}
            className={`px-8 py-3 rounded-xl font-bold text-lg flex items-center gap-3 transition-all ${
              selectedDeviceIds.size > 0
                ? "bg-primary-600 hover:bg-primary-500 text-white shadow-lg hover:shadow-primary-500/20 transform hover:-translate-y-1"
                : "bg-slate-800 text-slate-500 cursor-not-allowed"
            }`}
          >
            <Play className="w-5 h-5 fill-current" />
            Start Monitoring ({selectedDeviceIds.size})
          </button>
        </div>
      </div>
    </div>
  );
}
