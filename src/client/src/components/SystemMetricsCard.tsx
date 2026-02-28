import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createCollectMetricsJob, deviceJobsQueryKey, COLLECT_METRICS_COMMAND } from '../api/jobs';
import type { JobDto } from '../types/job';
import { JobStatus } from '../types/job';
import type { SystemMetricsDto } from '../types/systemMetrics';
import { parseSystemMetricsFromJson } from '../types/systemMetrics';

const JOB_STATUS_COMPLETED = JobStatus.Completed;
const JOB_STATUS_RUNNING = JobStatus.Running;

interface SystemMetricsCardProps {
  deviceId: string;
  jobs: JobDto[];
  isOnline: boolean;
}

function formatBytesMB(mb: number): string {
  if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`;
  return `${mb} MB`;
}

function formatBytesGB(gb: number): string {
  return `${gb.toFixed(1)} GB`;
}

function formatBytesPerSecond(bytesPerSecond: number): string {
  if (bytesPerSecond <= 0) return '0 B/s';
  const kb = bytesPerSecond / 1024;
  if (kb < 1024) return `${kb.toFixed(1)} KB/s`;
  const mb = kb / 1024;
  if (mb < 1024) return `${mb.toFixed(1)} MB/s`;
  const gb = mb / 1024;
  return `${gb.toFixed(1)} GB/s`;
}

function ProgressBar({ percent, label, valueLabel, variant = 'primary' }: {
  percent: number;
  label: string;
  valueLabel: string;
  variant?: 'primary' | 'success' | 'warning' | 'danger';
}) {
  const variantClass =
    variant === 'danger' ? 'bg-danger' :
    variant === 'warning' ? 'bg-warning' :
    variant === 'success' ? 'bg-success' :
    'bg-primary-500';
  const clamped = Math.min(100, Math.max(0, percent));

  return (
    <div className="mb-4 last:mb-0">
      <div className="flex justify-between text-xs mb-1">
        <span className="text-slate-400">{label}</span>
        <span className="text-slate-300">{valueLabel}</span>
      </div>
      <div className="w-full bg-slate-900 rounded-full h-2 overflow-hidden">
        <div
          className={`h-2 rounded-full transition-all duration-500 ${variantClass}`}
          style={{ width: `${clamped}%` }}
        />
      </div>
    </div>
  );
}

export function SystemMetricsCard({ deviceId, jobs, isOnline }: SystemMetricsCardProps) {
  const queryClient = useQueryClient();

  const collectMutation = useMutation({
    mutationFn: () => createCollectMetricsJob(deviceId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: deviceJobsQueryKey(deviceId) });
    },
  });

  const metricsJobs = jobs.filter(
    (j) => j.command === COLLECT_METRICS_COMMAND
  );
  const latestCompleted = metricsJobs.find((j) => j.status === JOB_STATUS_COMPLETED && j.output);
  const runningJob = metricsJobs.some((j) => j.status === JOB_STATUS_RUNNING);
  const metrics: SystemMetricsDto | null = latestCompleted?.output
    ? parseSystemMetricsFromJson(latestCompleted.output)
    : null;

  const isLoading = collectMutation.isPending || runningJob;
  const canCollect = isOnline && !runningJob;

  return (
    <div className="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-700 flex justify-between items-center flex-wrap gap-2">
        <div className="flex items-center gap-2">
          <h3 className="font-semibold text-white">System metrics</h3>
          {metrics && (
            <span className="text-xs text-slate-500 font-normal">
              Updated {new Date(metrics.timestamp).toLocaleString()}
            </span>
          )}
        </div>
        <button
          type="button"
          onClick={() => collectMutation.mutate()}
          disabled={!canCollect || isLoading}
          className="bg-primary-600 hover:bg-primary-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-1.5 rounded-lg text-xs font-medium shadow-lg shadow-primary-500/20 flex items-center gap-2 transition-colors"
        >
          {isLoading ? (
            <>
              <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
              </svg>
              Collecting…
            </>
          ) : (
            <>
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              {metrics ? 'Refresh metrics' : 'Collect metrics'}
            </>
          )}
        </button>
      </div>

      <div className="p-6">
        {!metrics && !isLoading && (
          <div className="text-center py-8 text-slate-400 text-sm">
            {!isOnline ? (
              <p>Device is offline. Metrics can be collected when the device is online.</p>
            ) : (
              <>
                <p className="mb-2">No metrics data yet.</p>
                <p className="text-slate-500 text-xs">Click &quot;Collect metrics&quot; to run the SystemInfo module on this device.</p>
              </>
            )}
          </div>
        )}

        {!metrics && runningJob && (
          <div className="flex items-center justify-center gap-3 py-8 text-slate-400 text-sm">
            <svg className="w-5 h-5 animate-spin" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
            </svg>
            <span>Collecting CPU, memory and disk info…</span>
          </div>
        )}

        {metrics && (
          <div className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="bg-slate-900/50 rounded-lg p-4 border border-slate-700/50">
                <div className="flex items-center gap-2 mb-2">
                  <div className="p-1.5 rounded bg-primary-600/20">
                    <svg className="w-4 h-4 text-primary-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2z" />
                    </svg>
                  </div>
                  <span className="text-slate-400 text-xs font-medium uppercase">CPU</span>
                </div>
                <div className="text-2xl font-bold text-white">{metrics.cpuLoad.toFixed(1)}%</div>
                <div className="text-xs text-slate-500 mt-1">Processor usage</div>
              </div>

              <div className="bg-slate-900/50 rounded-lg p-4 border border-slate-700/50">
                <div className="flex items-center gap-2 mb-2">
                  <div className="p-1.5 rounded bg-success/20">
                    <svg className="w-4 h-4 text-success" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4m0 5c0 2.21-3.582 4-8 4s-8-1.79-8-4" />
                    </svg>
                  </div>
                  <span className="text-slate-400 text-xs font-medium uppercase">Memory</span>
                </div>
                <div className="text-2xl font-bold text-white">{metrics.memoryInfo.usagePercent.toFixed(0)}%</div>
                <div className="text-xs text-slate-500 mt-1">
                  {formatBytesMB(metrics.memoryInfo.usedMB)} / {formatBytesMB(metrics.memoryInfo.totalMB)}
                </div>
              </div>

              <div className="bg-slate-900/50 rounded-lg p-4 border border-slate-700/50">
                <div className="flex items-center gap-2 mb-2">
                  <div className="p-1.5 rounded bg-purple-500/20">
                    <svg className="w-4 h-4 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 19a2 2 0 01-2-2V7a2 2 0 012-2h4a2 2 0 012 2v10a2 2 0 01-2 2H7a2 2 0 01-2-2z" />
                    </svg>
                  </div>
                  <span className="text-slate-400 text-xs font-medium uppercase">Disk</span>
                </div>
                <div className="text-2xl font-bold text-white">{metrics.diskInfo.usagePercent.toFixed(0)}%</div>
                <div className="text-xs text-slate-500 mt-1">
                  {formatBytesGB(metrics.diskInfo.usedGB)} used · {formatBytesGB(metrics.diskInfo.freeGB)} free
                  {metrics.diskInfo.driveName && ` (${metrics.diskInfo.driveName})`}
                </div>
              </div>
            </div>

            <ProgressBar
              label="Memory usage"
              valueLabel={`${metrics.memoryInfo.usedMB} / ${metrics.memoryInfo.totalMB} MB`}
              percent={metrics.memoryInfo.usagePercent}
              variant={metrics.memoryInfo.usagePercent > 90 ? 'danger' : metrics.memoryInfo.usagePercent > 75 ? 'warning' : 'success'}
            />
            <ProgressBar
              label="Disk usage"
              valueLabel={`${formatBytesGB(metrics.diskInfo.usedGB)} / ${formatBytesGB(metrics.diskInfo.totalGB)}`}
              percent={metrics.diskInfo.usagePercent}
              variant={metrics.diskInfo.usagePercent > 90 ? 'danger' : metrics.diskInfo.usagePercent > 85 ? 'warning' : 'primary'}
            />

            {metrics.diskInfo.volumes.length > 0 && (
              <div className="mt-2 border-t border-slate-700/50 pt-3">
                <h4 className="text-slate-400 uppercase text-xs font-semibold mb-2 tracking-wider">Disks</h4>
                <div className="space-y-1 text-xs text-slate-300">
                  {metrics.diskInfo.volumes.map((v) => (
                    <div key={v.name} className="flex justify-between gap-2">
                      <span className="font-mono">{v.name}</span>
                      <span className="text-slate-400">
                        {formatBytesGB(v.usedGB)} / {formatBytesGB(v.totalGB)} ({v.usagePercent.toFixed(0)}%)
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="pt-2 border-t border-slate-700/50">
              <h4 className="text-slate-400 uppercase text-xs font-semibold mb-3 tracking-wider">Network</h4>
              <div className="flex items-center justify-between text-sm">
                <div className="flex flex-col">
                  <span className="text-slate-500 text-xs">Received</span>
                  <span className="text-slate-300 font-mono text-xs">
                    {formatBytesPerSecond(metrics.networkInfo.totalBytesReceivedPerSecond)}
                  </span>
                </div>
                <div className="flex flex-col text-right">
                  <span className="text-slate-500 text-xs">Sent</span>
                  <span className="text-slate-300 font-mono text-xs">
                    {formatBytesPerSecond(metrics.networkInfo.totalBytesSentPerSecond)}
                  </span>
                </div>
              </div>
              {metrics.networkInfo.interfaces.length > 0 && (
                <div className="mt-2 grid grid-cols-1 md:grid-cols-2 gap-2 text-xs text-slate-400">
                  {metrics.networkInfo.interfaces.slice(0, 4).map((iface) => (
                    <div key={iface.name} className="flex flex-col">
                      <span className="text-slate-300 font-mono truncate">{iface.name}</span>
                      <span className="truncate">
                        ↓ {formatBytesPerSecond(iface.bytesReceivedPerSecond)} · ↑ {formatBytesPerSecond(iface.bytesSentPerSecond)}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="pt-2 border-t border-slate-700/50">
              <h4 className="text-slate-400 uppercase text-xs font-semibold mb-3 tracking-wider">System</h4>
              <div className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                <div className="flex justify-between py-1">
                  <span className="text-slate-500">OS</span>
                  <span className="text-slate-300">{metrics.systemInfo.oSDescription || metrics.systemInfo.oSPlatform || '—'}</span>
                </div>
                <div className="flex justify-between py-1">
                  <span className="text-slate-500">Architecture</span>
                  <span className="text-slate-300 font-mono text-xs">{metrics.systemInfo.oSArchitecture}</span>
                </div>
                <div className="flex justify-between py-1">
                  <span className="text-slate-500">Processors</span>
                  <span className="text-slate-300">{metrics.systemInfo.processorCount}</span>
                </div>
                <div className="flex justify-between py-1">
                  <span className="text-slate-500">Uptime</span>
                  <span className="text-slate-300">{metrics.systemInfo.uptime || '—'}</span>
                </div>
                <div className="flex justify-between py-1 col-span-2">
                  <span className="text-slate-500">Runtime</span>
                  <span className="text-slate-300 text-xs">{metrics.systemInfo.frameworkDescription}</span>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
