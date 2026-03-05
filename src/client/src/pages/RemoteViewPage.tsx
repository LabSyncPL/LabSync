import { useState, useEffect, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchDevices, devicesQueryKey } from "../api/devices";
import { RemoteViewSelection } from "../components/RemoteViewSelection";
import { MonitorWall } from "../components/MonitorWall";
import { useGridMonitor } from "../hooks/useGridMonitor";
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

  // 3. Monitor Hook
  const {
    subscribe,
    unsubscribe,
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
      subscribe(selectedDeviceIds).catch(console.error);
    }

    return () => {
      // Cleanup subscription when leaving wall or changing selection
      if (selectedDeviceIds.length > 0) {
        unsubscribe(selectedDeviceIds).catch(console.error);
      }
    };
  }, [viewMode, selectedDeviceIds, isPaused, subscribe, unsubscribe]);

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
