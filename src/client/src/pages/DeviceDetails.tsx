import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchDevices, devicesQueryKey } from "../api/devices";
import {
  getDeviceJobs,
  deviceJobsQueryKey,
  COLLECT_METRICS_COMMAND,
  createCollectMetricsJob,
} from "../api/jobs";
import { JobStatus, type JobDto } from "../types/job";
import { CreateJobModal } from "../components/CreateJobModal";
import { SystemMetricsCard } from "../components/SystemMetricsCard";
import { useSystemMetricsSettings } from "../settings/systemMetricsSettings";

// New Components
import { DeviceHeader } from "../components/DeviceDetails/DeviceHeader";
import { DeviceSystemInfoCard } from "../components/DeviceDetails/DeviceSystemInfoCard";
import { DeviceHardwareInfoCard } from "../components/DeviceDetails/DeviceHardwareInfoCard";
import { DeviceJobsCard } from "../components/DeviceDetails/DeviceJobsCard";
import { DeviceAgentLogsCard } from "../components/DeviceDetails/DeviceAgentLogsCard";

export function DeviceDetails() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [showCreateJob, setShowCreateJob] = useState(false);
  const [metricsSettings] = useSystemMetricsSettings();

  const { data: devices = [] } = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
  });

  const { data: jobs = [] } = useQuery({
    queryKey: deviceJobsQueryKey(id!),
    queryFn: () => getDeviceJobs(id!),
    enabled: !!id,
    refetchInterval: (query) => {
      const data = query.state.data as JobDto[] | undefined;
      const hasRunningCollectMetrics = data?.some(
        (j) => j.command === "CollectMetrics" && j.status === JobStatus.Running,
      );
      return hasRunningCollectMetrics ? 2000 : 5000;
    },
  });

  const device = devices.find((d) => d.id === id);

  // LiveUsage removed

  useEffect(() => {
    if (!id || !device?.isOnline) {
      return;
    }

    if (metricsSettings.autoMode !== "auto") {
      return;
    }

    const intervalMs =
      Math.max(metricsSettings.refreshIntervalSeconds, 5) * 1000;
    const intervalId = window.setInterval(() => {
      const hasRunningCollectMetrics = jobs.some(
        (j) =>
          j.command === COLLECT_METRICS_COMMAND &&
          j.status === JobStatus.Running,
      );
      if (!hasRunningCollectMetrics) {
        createCollectMetricsJob(id).catch(() => {});
      }
    }, intervalMs);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [
    id,
    device?.isOnline,
    jobs,
    metricsSettings.autoMode,
    metricsSettings.refreshIntervalSeconds,
  ]);

  if (!device) {
    return (
      <div className="flex flex-col h-full">
        <header className="h-16 border-b border-slate-800 flex items-center px-8 bg-slate-900 shrink-0">
          <h1 className="text-xl font-semibold text-white">Device Not Found</h1>
        </header>
        <div className="flex-1 p-8">
          <button
            onClick={() => navigate("/")}
            className="text-primary-400 hover:text-primary-300"
          >
            ← Back to Dashboard
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full bg-slate-900">
      <DeviceHeader device={device} />

      <div className="flex-1 overflow-y-auto p-8 scrollbar-dark">
        <div className="max-w-[1600px] mx-auto space-y-6">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
            {/* Left Column: System & Hardware */}
            <div className="lg:col-span-4 flex flex-col gap-6">
              <div className="flex-none">
                <DeviceSystemInfoCard device={device} />
              </div>
              <div className="flex-1">
                <DeviceHardwareInfoCard device={device} />
              </div>
            </div>

            {/* Right Column: Detailed Metrics */}
            <div className="lg:col-span-8">
              <SystemMetricsCard
                deviceId={device.id}
                jobs={jobs}
                isOnline={device.isOnline}
              />
            </div>
          </div>

          {/* Bottom Row: Jobs & Logs */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 h-[560px]">
            <div className="lg:col-span-5 h-full">
              <DeviceJobsCard
                jobs={jobs}
                device={device}
                onNewJob={() => setShowCreateJob(true)}
              />
            </div>
            <div className="lg:col-span-7 h-full">
              <DeviceAgentLogsCard />
            </div>
          </div>
        </div>
      </div>

      {showCreateJob && device && (
        <CreateJobModal
          deviceId={device.id}
          onClose={() => setShowCreateJob(false)}
          onSuccess={() => {
            queryClient.invalidateQueries({
              queryKey: deviceJobsQueryKey(device.id),
            });
          }}
        />
      )}
    </div>
  );
}
