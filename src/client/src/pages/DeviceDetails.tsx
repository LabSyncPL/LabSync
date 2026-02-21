import { useEffect, useMemo, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { fetchDevices, devicesQueryKey } from "../api/devices";
import {
  getDeviceJobs,
  deviceJobsQueryKey,
  COLLECT_METRICS_COMMAND,
  createCollectMetricsJob,
} from "../api/jobs";
import { DEVICE_PLATFORM_LABELS } from "../types/device";
import { JOB_STATUS_LABELS, type JobDto } from "../types/job";
import { CreateJobModal } from "../components/CreateJobModal";
import { SystemMetricsCard } from "../components/SystemMetricsCard";
import type { SystemMetricsDto } from "../types/systemMetrics";
import { parseSystemMetricsFromJson } from "../types/systemMetrics";

function formatLastSeen(value: string | null): string {
  if (value == null) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins} min ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  return date.toLocaleDateString();
}

function getPlatformIcon(platform: number) {
  switch (platform) {
    case 1:
      return (
        <svg className="w-8 h-8" fill="currentColor" viewBox="0 0 24 24">
          <path d="M0 3.449L9.75 2.1v9.451H0m10.949-9.602L24 0v11.4h-13.051M0 12.6h9.75v9.451L0 20.699M10.949 12.6H24V24l-12.9-1.801" />
        </svg>
      );
    case 2:
      return (
        <svg className="w-8 h-8" fill="currentColor" viewBox="0 0 24 24">
          <path d="M12 0c-6.627 0-12 5.373-12 12s5.373 12 12 12 12-5.373 12-12-5.373-12-12-12zm4.333 3.667c.736 0 1.333.597 1.333 1.333 0 .736-.597 1.333-1.333 1.333-.736 0-1.333-.597-1.333-1.333 0-.736.597-1.333 1.333-1.333zm-8.667 0c.736 0 1.333.597 1.333 1.333 0 .736-.597 1.333-1.333 1.333-.736 0-1.333-.597-1.333-1.333 0-.736.597-1.333 1.333-1.333zm9.056 12.333h-1.333v2.667h-2.667v-2.667h-1.333v2.667h-2.667v-2.667h-1.389v-4h10.778v4z" />
        </svg>
      );
    default:
      return null;
  }
}

type MetricsHistoryPoint = {
  timestamp: number;
  cpu: number;
  memoryPercent: number;
  diskPercent: number;
  netRx: number;
  netTx: number;
};

function Sparkline({
  values,
  colorClass,
}: {
  values: number[];
  colorClass: string;
}) {
  const count = values.length;
  if (count === 0) {
    return (
      <div className="w-full h-16 flex items-center justify-center text-xs text-slate-500">
        No data yet
      </div>
    );
  }

  const max = Math.max(...values, 1);
  const points = values
    .map((v, index) => {
      const x = (index / Math.max(count - 1, 1)) * 100;
      const y = 100 - (v / max) * 100;
      return `${x},${y}`;
    })
    .join(" ");

  return (
    <svg viewBox="0 0 100 100" className="w-full h-16 text-primary-400">
      <polyline
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        className={colorClass}
        points={points}
      />
    </svg>
  );
}

function formatBytesPerSecond(value: number): string {
  if (value <= 0) return "0 B/s";
  const kb = value / 1024;
  if (kb < 1024) return `${kb.toFixed(1)} KB/s`;
  const mb = kb / 1024;
  if (mb < 1024) return `${mb.toFixed(1)} MB/s`;
  const gb = mb / 1024;
  return `${gb.toFixed(1)} GB/s`;
}

export function DeviceDetails() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [showCreateJob, setShowCreateJob] = useState(false);
  const [metricsHistory, setMetricsHistory] = useState<MetricsHistoryPoint[]>(
    [],
  );

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
        (j) => j.command === "CollectMetrics" && j.status === 1,
      );
      return hasRunningCollectMetrics ? 2000 : 5000;
    },
  });

  const device = devices.find((d) => d.id === id);

  const metricsJobs = useMemo(
    () => jobs.filter((j) => j.command === COLLECT_METRICS_COMMAND),
    [jobs],
  );

  const latestMetricsJob = useMemo(
    () => metricsJobs.find((j) => j.status === 2 && j.output),
    [metricsJobs],
  );

  const latestMetrics: SystemMetricsDto | null = useMemo(
    () =>
      latestMetricsJob?.output
        ? parseSystemMetricsFromJson(latestMetricsJob.output)
        : null,
    [latestMetricsJob],
  );

  useEffect(() => {
    if (!latestMetrics) {
      return;
    }

    const timestamp = new Date(latestMetrics.timestamp).getTime();
    setMetricsHistory((prev) => {
      const last = prev[prev.length - 1];
      if (last && last.timestamp === timestamp) {
        return prev;
      }

      const next: MetricsHistoryPoint = {
        timestamp,
        cpu: latestMetrics.cpuLoad,
        memoryPercent: latestMetrics.memoryInfo.usagePercent,
        diskPercent: latestMetrics.diskInfo.usagePercent,
        netRx: latestMetrics.networkInfo.totalBytesReceivedPerSecond,
        netTx: latestMetrics.networkInfo.totalBytesSentPerSecond,
      };

      const merged = [...prev, next];
      if (merged.length > 60) {
        return merged.slice(merged.length - 60);
      }
      return merged;
    });
  }, [latestMetrics]);

  useEffect(() => {
    if (!id || !device?.isOnline) {
      return;
    }

    let intervalId: number | undefined;

    intervalId = window.setInterval(() => {
      const hasRunningCollectMetrics = jobs.some(
        (j) => j.command === COLLECT_METRICS_COMMAND && j.status === 1,
      );
      if (!hasRunningCollectMetrics) {
        createCollectMetricsJob(id).catch(() => {});
      }
    }, 15000);

    return () => {
      if (intervalId !== undefined) {
        window.clearInterval(intervalId);
      }
    };
  }, [id, device?.isOnline, jobs]);

  if (!device) {
    return (
      <>
        <header className="h-16 border-b border-slate-800 flex items-center px-8 bg-slate-900">
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
      </>
    );
  }

  return (
    <>
      <header className="h-20 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <div>
          <div className="flex items-center text-xs text-slate-500 mb-1">
            <button
              onClick={() => navigate("/")}
              className="hover:text-slate-300 cursor-pointer"
            >
              Dashboard
            </button>
            <svg
              className="w-3 h-3 mx-2"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M9 5l7 7-7 7"
              ></path>
            </svg>
            <span className="text-slate-300">Device Details</span>
          </div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-white tracking-tight">
              {device.hostname}
            </h1>
            {device.isOnline ? (
              <span className="px-2 py-0.5 rounded text-xs font-semibold bg-success/20 text-success border border-success/20">
                Online
              </span>
            ) : (
              <span className="px-2 py-0.5 rounded text-xs font-semibold bg-slate-700 text-slate-300 border border-slate-600">
                Offline
              </span>
            )}
            {!device.isApproved && (
              <span className="px-2 py-0.5 rounded text-xs font-semibold bg-warning/20 text-warning border border-warning/20">
                Pending Approval
              </span>
            )}
          </div>
        </div>
        <div className="flex items-center gap-3">
          <button className="bg-slate-800 hover:bg-slate-700 text-slate-300 px-4 py-2 rounded-lg border border-slate-700 font-medium text-sm flex items-center transition-colors">
            <svg
              className="w-4 h-4 mr-2"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
              ></path>
            </svg>
            Restart Agent
          </button>
          <button className="bg-primary-600 hover:bg-primary-500 text-white px-5 py-2 rounded-lg font-semibold shadow-lg shadow-primary-500/25 flex items-center transition-all">
            <svg
              className="w-4 h-4 mr-2"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z"
              ></path>
            </svg>
            Remote View (VNC)
          </button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto p-8">
        <div className="grid grid-cols-12 gap-6">
          <div className="col-span-12 lg:col-span-4 space-y-6">
            <div className="bg-slate-800 rounded-xl border border-slate-700 p-6">
              <h3 className="text-slate-400 uppercase text-xs font-semibold mb-4 tracking-wider">
                System Information
              </h3>
              <div className="flex items-center mb-6">
                <div className="w-12 h-12 bg-blue-900/30 rounded-lg flex items-center justify-center text-blue-400 mr-4">
                  {getPlatformIcon(device.platform)}
                </div>
                <div>
                  <p className="text-white font-medium text-lg">
                    {device.osVersion}
                  </p>
                  <p className="text-slate-500 text-xs">
                    {DEVICE_PLATFORM_LABELS[device.platform]}
                  </p>
                </div>
              </div>
              <div className="space-y-3">
                <div className="flex justify-between py-2 border-b border-slate-700/50">
                  <span className="text-slate-400">IP Address</span>
                  <span className="text-white font-mono text-xs">
                    {device.ipAddress ?? "—"}
                  </span>
                </div>
                <div className="flex justify-between py-2 border-b border-slate-700/50">
                  <span className="text-slate-400">MAC Address</span>
                  <span className="text-white font-mono text-xs">
                    {device.macAddress}
                  </span>
                </div>
                <div className="flex justify-between py-2">
                  <span className="text-slate-400">Last Seen</span>
                  <span
                    className={
                      device.isOnline ? "text-success" : "text-slate-400"
                    }
                  >
                    {formatLastSeen(device.lastSeenAt)}
                  </span>
                </div>
              </div>
            </div>

            {device.hardwareInfo && (
              <div className="bg-slate-800 rounded-xl border border-slate-700 p-6">
                <h3 className="text-slate-400 uppercase text-xs font-semibold mb-4 tracking-wider">
                  Hardware Specs
                </h3>
                <div className="text-slate-300 text-sm font-mono text-xs whitespace-pre-wrap">
                  {device.hardwareInfo}
                </div>
              </div>
            )}
          </div>

          <div className="col-span-12 lg:col-span-8 space-y-6">
            <SystemMetricsCard
              deviceId={device.id}
              jobs={jobs}
              isOnline={device.isOnline}
            />

            {latestMetrics && (
              <div className="bg-slate-800 rounded-xl border border-slate-700 p-6">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="font-semibold text-white">
                    Live resource usage
                  </h3>
                  <span className="text-xs text-slate-500">
                    Samples: {metricsHistory.length}
                  </span>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="bg-slate-900/60 rounded-lg p-4 border border-slate-700/60 flex flex-col">
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-xs font-semibold uppercase text-slate-400">
                        CPU
                      </span>
                      <span className="text-lg font-bold text-white">
                        {latestMetrics.cpuLoad.toFixed(1)}%
                      </span>
                    </div>
                    <Sparkline
                      values={metricsHistory.map((p) => p.cpu)}
                      colorClass="text-primary-400"
                    />
                  </div>
                  <div className="bg-slate-900/60 rounded-lg p-4 border border-slate-700/60 flex flex-col">
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-xs font-semibold uppercase text-slate-400">
                        Memory
                      </span>
                      <span className="text-lg font-bold text-white">
                        {latestMetrics.memoryInfo.usagePercent.toFixed(0)}%
                      </span>
                    </div>
                    <Sparkline
                      values={metricsHistory.map((p) => p.memoryPercent)}
                      colorClass="text-success"
                    />
                  </div>
                  <div className="bg-slate-900/60 rounded-lg p-4 border border-slate-700/60 flex flex-col">
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-xs font-semibold uppercase text-slate-400">
                        Network
                      </span>
                      <div className="text-right">
                        <div className="text-xs text-slate-400">
                          ↓{" "}
                          {formatBytesPerSecond(
                            latestMetrics.networkInfo
                              .totalBytesReceivedPerSecond,
                          )}
                        </div>
                        <div className="text-xs text-slate-400">
                          ↑{" "}
                          {formatBytesPerSecond(
                            latestMetrics.networkInfo.totalBytesSentPerSecond,
                          )}
                        </div>
                      </div>
                    </div>
                    <Sparkline
                      values={metricsHistory.map((p) => p.netRx / 1024)}
                      colorClass="text-blue-400"
                    />
                  </div>
                </div>
              </div>
            )}

            <div className="bg-slate-800 rounded-xl border border-slate-700">
              <div className="px-6 py-4 border-b border-slate-700 flex justify-between items-center">
                <h3 className="font-semibold text-white">Recent Jobs</h3>
                {device?.isApproved && (
                  <button
                    onClick={() => setShowCreateJob(true)}
                    className="bg-primary-600 hover:bg-primary-500 text-white px-4 py-1.5 rounded-lg text-xs font-medium shadow-lg shadow-primary-500/20 flex items-center transition-colors"
                  >
                    <svg
                      className="w-4 h-4 mr-1.5"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth="2"
                        d="M12 6v6m0 0v6m0-6h6m-6 0H6"
                      ></path>
                    </svg>
                    New Job
                  </button>
                )}
              </div>
              {jobs.length === 0 ? (
                <div className="p-8 text-center text-slate-400 text-sm">
                  No jobs yet. Create a job to execute commands on this device.
                </div>
              ) : (
                <div className="divide-y divide-slate-700">
                  {jobs.map((job) => {
                    const statusColor =
                      job.status === 2
                        ? "text-success"
                        : job.status === 3
                          ? "text-danger"
                          : job.status === 1
                            ? "text-primary-400"
                            : "text-warning";
                    return (
                      <div
                        key={job.id}
                        className="px-6 py-4 hover:bg-slate-700/30 transition-colors"
                      >
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <span className="font-medium text-white">
                                {job.command}
                              </span>
                              <span
                                className={`text-xs font-medium ${statusColor}`}
                              >
                                {JOB_STATUS_LABELS[job.status]}
                              </span>
                            </div>
                            {job.arguments && (
                              <p className="text-slate-400 text-xs font-mono mb-2">
                                {job.arguments}
                              </p>
                            )}
                            {job.output && (
                              <div className="mt-2 p-2 bg-slate-900 rounded text-xs font-mono text-slate-300 max-h-20 overflow-y-auto">
                                {job.output}
                              </div>
                            )}
                          </div>
                          <div className="ml-4 text-right text-xs text-slate-500">
                            <div>
                              {new Date(job.createdAt).toLocaleString()}
                            </div>
                            {job.exitCode !== null && (
                              <div className="mt-1">Exit: {job.exitCode}</div>
                            )}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            <div className="bg-slate-800 rounded-xl border border-slate-700 flex flex-col h-64">
              <div className="px-6 py-3 border-b border-slate-700 flex space-x-6">
                <button className="text-primary-400 font-medium text-xs uppercase border-b-2 border-primary-500 py-3 -mb-3.5">
                  Agent Logs
                </button>
                <button className="text-slate-400 hover:text-white font-medium text-xs uppercase py-3">
                  Installed Software
                </button>
                <button className="text-slate-400 hover:text-white font-medium text-xs uppercase py-3">
                  Processes
                </button>
              </div>
              <div className="flex-1 bg-console p-4 font-mono text-xs overflow-y-auto rounded-b-xl">
                <div className="space-y-1 text-slate-300">
                  <div className="flex">
                    <span className="text-slate-500 min-w-[140px]">
                      {new Date().toLocaleString()}
                    </span>
                    <span className="text-blue-400 mr-2">[INFO]</span>
                    <span>Device connected to SignalR Hub.</span>
                  </div>
                  <div className="flex opacity-50">
                    <span className="text-slate-500 min-w-[140px]">&gt;_</span>
                    <span className="text-slate-300 animate-pulse">
                      Waiting for commands...
                    </span>
                  </div>
                </div>
              </div>
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
    </>
  );
}
