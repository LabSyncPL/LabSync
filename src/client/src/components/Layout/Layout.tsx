import { useEffect } from "react";
import { Outlet } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Sidebar } from "./Sidebar";
import { fetchDevices, devicesQueryKey } from "../../api/devices";
import { useSystemMetricsSettings } from "../../settings/systemMetricsSettings";
import { createCollectMetricsJob } from "../../api/jobs";

export function Layout() {
  const [metricsSettings] = useSystemMetricsSettings();
  const { data: devices = [] } = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
    refetchInterval:
      metricsSettings.autoMode === "background"
        ? Math.max(metricsSettings.refreshIntervalSeconds, 5) * 1000
        : false,
  });

  useEffect(() => {
    if (metricsSettings.autoMode !== "background") {
      return;
    }

    const onlineDevices = devices.filter(
      (d) => d.isOnline && d.isApproved,
    );
    if (onlineDevices.length === 0) {
      return;
    }

    const intervalMs =
      Math.max(metricsSettings.refreshIntervalSeconds, 5) * 1000;
    const intervalId = window.setInterval(() => {
      onlineDevices.forEach((device) => {
        createCollectMetricsJob(device.id).catch(() => {});
      });
    }, intervalMs);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [devices, metricsSettings.autoMode, metricsSettings.refreshIntervalSeconds]);

  return (
    <div className="flex h-screen overflow-hidden text-sm">
      <Sidebar />
      <main className="flex-1 flex flex-col bg-slate-900 overflow-hidden">
        <Outlet />
      </main>
    </div>
  );
}
