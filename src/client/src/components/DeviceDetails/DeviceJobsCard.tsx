import { JOB_STATUS_LABELS } from "../../types/job";
import type { JobDto } from "../../types/job";
import type { DeviceDto } from "../../types/device";

interface DeviceJobsCardProps {
  jobs: JobDto[];
  device: DeviceDto | undefined;
  onNewJob: () => void;
}

export function DeviceJobsCard({
  jobs,
  device,
  onNewJob,
}: DeviceJobsCardProps) {
  return (
    <div className="bg-slate-800 rounded-2xl border border-slate-700 overflow-hidden flex flex-col h-full shadow-sm hover:shadow-md transition-shadow">
      <div className="px-6 py-5 border-b border-slate-700/50 flex justify-between items-center bg-slate-800">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-slate-700/30 rounded-lg text-slate-400 border border-slate-700/50">
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
              />
            </svg>
          </div>
          <h3 className="text-sm font-bold text-white uppercase tracking-wider">
            Recent Jobs
          </h3>
          <span className="bg-slate-700/50 text-slate-300 px-2 py-0.5 rounded-full text-xs font-medium border border-slate-600/50">
            {jobs.length}
          </span>
        </div>
        {device?.isApproved && (
          <button
            onClick={onNewJob}
            className="bg-primary-600/10 hover:bg-primary-600/20 text-primary-400 hover:text-primary-300 px-3 py-1.5 rounded-lg text-xs font-bold border border-primary-500/20 flex items-center transition-all hover:scale-105 active:scale-95"
          >
            <svg
              className="w-3.5 h-3.5 mr-1.5"
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
            NEW JOB
          </button>
        )}
      </div>

      <div className="flex-1 overflow-y-auto max-h-[400px] scrollbar-dark bg-slate-800/50">
        {jobs.length === 0 ? (
          <div className="p-8 text-center flex flex-col items-center justify-center h-48">
            <div className="w-16 h-16 bg-slate-700/20 rounded-full flex items-center justify-center mb-4 border border-slate-700/30">
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
                  d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"
                />
              </svg>
            </div>
            <p className="text-slate-300 text-sm font-medium mb-1">
              No jobs history
            </p>
            <p className="text-slate-500 text-xs max-w-[200px]">
              Create a new job to execute commands on this device.
            </p>
          </div>
        ) : (
          <div className="divide-y divide-slate-700/50">
            {jobs.map((job) => {
              const statusColor =
                job.status === 2
                  ? "text-success bg-success/5 border-success/20 ring-success/10"
                  : job.status === 3
                    ? "text-danger bg-danger/5 border-danger/20 ring-danger/10"
                    : job.status === 1
                      ? "text-primary-400 bg-primary-500/5 border-primary-500/20 ring-primary-500/10"
                      : "text-warning bg-warning/5 border-warning/20 ring-warning/10";

              return (
                <div
                  key={job.id}
                  className="px-6 py-4 hover:bg-slate-700/30 transition-colors group border-l-2 border-l-transparent hover:border-l-primary-500"
                >
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex items-center gap-3">
                      <span
                        className={`px-2 py-0.5 rounded text-[10px] uppercase font-bold border ring-1 ring-inset ${statusColor} tracking-wider shadow-sm`}
                      >
                        {JOB_STATUS_LABELS[job.status]}
                      </span>
                      <span
                        className="font-mono text-sm text-white font-medium truncate max-w-[200px]"
                        title={job.command}
                      >
                        {job.command}
                      </span>
                    </div>
                    <span className="text-[10px] text-slate-500 font-medium bg-slate-800/80 px-1.5 py-0.5 rounded border border-slate-700/50">
                      {new Date(job.createdAt).toLocaleString()}
                    </span>
                  </div>

                  {job.arguments && (
                    <div className="text-xs font-mono text-slate-400 mb-3 pl-3 border-l-2 border-slate-700/50 mt-1">
                      <span className="text-slate-600 mr-2 select-none">$</span>
                      <span className="text-slate-300">{job.arguments}</span>
                    </div>
                  )}

                  {job.output && (
                    <div className="mt-3 bg-slate-950 rounded-lg border border-slate-800 p-3 font-mono text-[11px] text-slate-400 whitespace-pre-wrap max-h-32 overflow-y-auto scrollbar-dark shadow-inner group-hover:border-slate-700/50 transition-colors">
                      {job.output}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
