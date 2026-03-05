import { useState, useEffect, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchDevices, devicesQueryKey } from "../api/devices";
import { RemoteViewSelection } from "../components/RemoteViewSelection";
import { MonitorWall } from "../components/MonitorWall";
import { useGridMonitor, type MonitorSettings } from "../hooks/useGridMonitor";
import { RemoteControlModal } from "../components/RemoteControl/RemoteControlModal";
import type { DeviceDto } from "../types/device";

export function RemoteViewPage() {
  // 1. Data Fetching
  const { data: allDevices = [] } = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
    refetchInterval: 30000, // Refresh device list periodically
  });

  // 2. Local State
  const [viewMode, setViewMode] = useState<"selection" | "wall">("selection");
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<string[]>([]);
  const [remoteControlDeviceId, setRemoteControlDeviceId] = useState<
    string | null
  >(null);
  const [isPaused, setIsPaused] = useState(false);
  const [monitorSettings, setMonitorSettings] = useState<MonitorSettings>({
    width: 400,
    quality: 60,
    fps: 1,
  });

  // 3. Monitor Hook
  const {
    subscribe,
    unsubscribe,
    configure,
    images,
  } = useGridMonitor();

  // 4. Computed
  const selectedDevices = useMemo(() => {
    const idSet = new Set(selectedDeviceIds);
    return allDevices.filter((d) => idSet.has(d.id));
  }, [allDevices, selectedDeviceIds]);

  // 5. Effects

  // Handle Subscription
  useEffect(() => {
    if (viewMode === "wall" && selectedDeviceIds.length > 0 && !isPaused) {
      subscribe(selectedDeviceIds)
        .then(() => configure(selectedDeviceIds, monitorSettings))
        .catch(console.error);
    }

    return () => {
      // Cleanup subscription when leaving wall or changing selection
      if (selectedDeviceIds.length > 0) {
        unsubscribe(selectedDeviceIds).catch(console.error);
      }
    };
  }, [viewMode, selectedDeviceIds, isPaused, subscribe, unsubscribe]);

  // Handle Settings Change
  useEffect(() => {
    if (viewMode === "wall" && selectedDeviceIds.length > 0 && !isPaused) {
      configure(selectedDeviceIds, monitorSettings).catch(console.error);
    }
  }, [monitorSettings, configure]); // selectedDeviceIds/viewMode/isPaused are stable or handled by other effect logic

  // 6. Handlers
  const handleStartMonitoring = (ids: string[]) => {
    setSelectedDeviceIds(ids);
    setViewMode("wall");
    setIsPaused(false);
  };

  const handleBackToSelection = () => {
    setViewMode("selection");
    setSelectedDeviceIds([]);
    setIsPaused(false);
  };

  const handleDeviceDoubleClick = (device: DeviceDto) => {
    setRemoteControlDeviceId(device.id);
  };

  const handleCloseRemoteControl = () => {
    setRemoteControlDeviceId(null);
  };

  // 7. Render
  return (
    <div className="h-full flex flex-col relative">
      {viewMode === "selection" ? (
        <RemoteViewSelection
          devices={allDevices}
          onStartMonitoring={handleStartMonitoring}
        />
      ) : (
        <MonitorWall
          devices={selectedDevices}
          onBack={handleBackToSelection}
          onDeviceDoubleClick={handleDeviceDoubleClick}
          isPaused={isPaused}
          togglePause={() => setIsPaused((prev) => !prev)}
          images={images}
          currentSettings={monitorSettings}
          onUpdateSettings={setMonitorSettings}
        />
      )}

      {/* Full Remote Control Modal Overlay */}
      {remoteControlDeviceId && (
        <RemoteControlModal
          deviceId={remoteControlDeviceId}
          onClose={handleCloseRemoteControl}
        />
      )}
    </div>
  );
}
