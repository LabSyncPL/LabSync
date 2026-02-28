import type { DeviceDto } from "../../types/device";

interface DeviceHardwareInfoCardProps {
  device: DeviceDto;
}

export function DeviceHardwareInfoCard({
  device,
}: DeviceHardwareInfoCardProps) {
  return (
    <div className="bg-slate-800 rounded-2xl border border-slate-700 p-6 flex flex-col h-full shadow-sm hover:shadow-md transition-shadow">
      <div className="flex items-center gap-3 mb-6">
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
          Hardware Specs {device.hostname}
        </h3>
      </div>
    </div>
  );
}
