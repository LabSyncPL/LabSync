import type { DeviceDto } from "../../types/device";
import { DEVICE_PLATFORM_LABELS } from "../../types/device";
import { formatLastSeen, getPlatformIcon } from "../../utils/deviceUtils";

interface DeviceSystemInfoCardProps {
  device: DeviceDto;
}

export function DeviceSystemInfoCard({ device }: DeviceSystemInfoCardProps) {
  return (
    <div className="bg-slate-800 rounded-2xl border border-slate-700 p-6 flex flex-col h-full shadow-sm hover:shadow-md transition-shadow">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2.5 bg-slate-700/30 rounded-lg text-slate-400 border border-slate-700/50">
          <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2z" />
          </svg>
        </div>
        <h3 className="text-sm font-bold text-white uppercase tracking-wider">System Info</h3>
      </div>

      <div className="space-y-4 flex-1">
        <div className="flex items-center justify-between py-2 border-b border-slate-700/30">
          <span className="text-slate-400 text-sm font-medium">Platform</span>
          <div className="flex items-center gap-2 text-white text-sm font-medium bg-slate-700/20 px-2 py-1 rounded">
            {getPlatformIcon(device.platform, "w-4 h-4 text-slate-400")}
            {DEVICE_PLATFORM_LABELS[device.platform]}
          </div>
        </div>
        
        <div className="flex items-center justify-between py-2 border-b border-slate-700/30">
          <span className="text-slate-400 text-sm font-medium">OS Version</span>
          <span className="text-white text-sm font-medium text-right max-w-[60%] truncate" title={device.osVersion}>
            {device.osVersion}
          </span>
        </div>

        <div className="flex items-center justify-between py-2 border-b border-slate-700/30">
          <span className="text-slate-400 text-sm font-medium">IP Address</span>
          <span className="text-white text-xs font-mono bg-slate-900 px-2 py-1 rounded border border-slate-700/50">
            {device.ipAddress ?? "—"}
          </span>
        </div>

        <div className="flex items-center justify-between py-2 border-b border-slate-700/30">
          <span className="text-slate-400 text-sm font-medium">MAC Address</span>
          <span className="text-white text-xs font-mono bg-slate-900 px-2 py-1 rounded border border-slate-700/50">
            {device.macAddress}
          </span>
        </div>

        <div className="flex items-center justify-between py-2 pt-3">
          <span className="text-slate-400 text-sm font-medium">Last Seen</span>
          <span className={`text-sm font-semibold ${device.isOnline ? "text-success" : "text-slate-500"}`}>
            {formatLastSeen(device.lastSeenAt)}
          </span>
        </div>
      </div>
    </div>
  );
}
