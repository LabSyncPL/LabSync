import { useState, useEffect } from "react";
import type { DeviceDto } from "../../types/device";
import { createGetHardwareSpecsJob, getJob } from "../../api/jobs";
import { JobStatus } from "../../types/job";

interface HardwareInfo {
  CpuName: string;
  GpuName: string;
  TotalRam: string;
  Disks: { Model: string; Size: string; Type: string }[];
  NetworkAdapters: { Name: string; MacAddress: string; Status: string }[];
}

interface DeviceHardwareInfoCardProps {
  device: DeviceDto;
}

export function DeviceHardwareInfoCard({
  device,
}: DeviceHardwareInfoCardProps) {
  const [specs, setSpecs] = useState<HardwareInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadSpecs = async () => {
    setLoading(true);
    setError(null);
    try {
      const job = await createGetHardwareSpecsJob(device.id);
      let currentJob = job;

      while (
        currentJob.status === JobStatus.Pending ||
        currentJob.status === JobStatus.Running
      ) {
        await new Promise((resolve) => setTimeout(resolve, 1000));
        currentJob = await getJob(device.id, currentJob.id);
      }

      if (currentJob.status === JobStatus.Completed && currentJob.output) {
        try {
          const parsed = JSON.parse(currentJob.output);
          setSpecs(parsed);
        } catch (e) {
          setError("Failed to parse hardware specs.");
        }
      } else {
        setError(`Job failed: ${currentJob.output || "Unknown error"}`);
      }
    } catch (err) {
      setError("Failed to start or monitor job.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSpecs();
  }, [device.id]);

  return (
    <div className="bg-slate-800 rounded-2xl border border-slate-700 p-6 flex flex-col h-full shadow-sm hover:shadow-md transition-shadow">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-slate-700/30 rounded-lg text-slate-400 border border-slate-700/50">
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
            Hardware Specs
          </h3>
        </div>
        {!specs && !loading && (
          <button
            onClick={loadSpecs}
            className="px-3 py-1.5 bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium rounded transition-colors"
          >
            Load Specs
          </button>
        )}
      </div>

      <div className="flex-1 space-y-4 overflow-y-auto max-h-[400px] custom-scrollbar">
        {loading && (
          <div className="text-center py-8 text-slate-400 animate-pulse">
            Loading hardware information...
          </div>
        )}

        {error && (
          <div className="text-center py-4 text-red-400 text-sm">
            {error}
            <button
              onClick={loadSpecs}
              className="block mx-auto mt-2 text-blue-400 hover:text-blue-300 underline"
            >
              Retry
            </button>
          </div>
        )}

        {specs && (
          <div className="space-y-4">
            <div className="border-b border-slate-700/50 pb-2">
              <div className="text-xs text-slate-400 uppercase tracking-wide mb-1">
                Processor
              </div>
              <div className="text-white font-medium text-sm break-words">
                {specs.CpuName}
              </div>
            </div>

            <div className="border-b border-slate-700/50 pb-2">
              <div className="text-xs text-slate-400 uppercase tracking-wide mb-1">
                Memory
              </div>
              <div className="text-white font-medium text-sm">
                {specs.TotalRam}
              </div>
            </div>

            <div className="border-b border-slate-700/50 pb-2">
              <div className="text-xs text-slate-400 uppercase tracking-wide mb-1">
                Graphics
              </div>
              <div className="text-white font-medium text-sm break-words">
                {specs.GpuName || "N/A"}
              </div>
            </div>

            <div className="border-b border-slate-700/50 pb-2">
              <div className="text-xs text-slate-400 uppercase tracking-wide mb-1">
                Storage
              </div>
              {specs.Disks.map((disk, i) => (
                <div
                  key={i}
                  className="text-white font-medium text-sm flex justify-between"
                >
                  <span className="truncate pr-2" title={disk.Model}>
                    {disk.Model}
                  </span>
                  <span className="text-slate-400 whitespace-nowrap">
                    {disk.Size}
                  </span>
                </div>
              ))}
              {specs.Disks.length === 0 && (
                <div className="text-slate-500 text-sm">No disks detected</div>
              )}
            </div>
          </div>
        )}

        {!specs && !loading && !error && (
          <div className="text-center py-8 text-slate-500 text-sm">
            Click load to view hardware specifications.
          </div>
        )}
      </div>
    </div>
  );
}
