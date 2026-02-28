import { useNavigate } from "react-router-dom";
import type { DeviceDto } from "../../types/device";
import { DEVICE_PLATFORM_LABELS } from "../../types/device";
import { formatLastSeen, getPlatformIcon } from "../../utils/deviceUtils";

interface DeviceGridCardProps {
  device: DeviceDto;
  onApprove: (e: React.MouseEvent, device: DeviceDto) => void;
  isApproving: boolean;
}

export function DeviceGridCard({ device, onApprove, isApproving }: DeviceGridCardProps) {
  const navigate = useNavigate();

  return (
    <div
      onClick={() => navigate(`/devices/${device.id}`)}
      className={`group bg-slate-800 rounded-2xl border p-5 flex flex-col h-full shadow-sm hover:shadow-xl transition-all cursor-pointer relative overflow-hidden ${
        !device.isApproved 
          ? "border-warning/30 hover:border-warning/50 bg-gradient-to-b from-slate-800 to-warning/5" 
          : "border-slate-700 hover:border-primary-500/50 hover:translate-y-[-2px]"
      }`}
    >
      <div className="flex justify-between items-start mb-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-slate-700/50 flex items-center justify-center text-slate-400 border border-slate-600/30 group-hover:bg-slate-700 group-hover:text-white transition-colors">
            {getPlatformIcon(device.platform)}
          </div>
          <div>
            <h3 className="font-bold text-white text-sm truncate max-w-[140px] group-hover:text-primary-400 transition-colors">
              {device.hostname}
            </h3>
            <p className="text-xs text-slate-500 font-medium truncate max-w-[120px]">
              {DEVICE_PLATFORM_LABELS[device.platform]} {device.osVersion}
            </p>
          </div>
        </div>
        
        {device.isOnline ? (
          <div className="w-2.5 h-2.5 bg-success rounded-full shadow-[0_0_8px_rgba(34,197,94,0.6)] animate-pulse" title="Online" />
        ) : (
          <div className="w-2.5 h-2.5 bg-slate-600 rounded-full border border-slate-500" title="Offline" />
        )}
      </div>

      <div className="space-y-2 mb-4 flex-1">
        <div className="flex justify-between items-center text-xs">
          <span className="text-slate-500">IP Address</span>
          <span className="text-slate-300 font-mono bg-slate-900/50 px-1.5 py-0.5 rounded border border-slate-800">
            {device.ipAddress ?? "—"}
          </span>
        </div>
        <div className="flex justify-between items-center text-xs">
          <span className="text-slate-500">Last Seen</span>
          <span className={`${device.isOnline ? "text-success" : "text-slate-400"}`}>
            {formatLastSeen(device.lastSeenAt)}
          </span>
        </div>
      </div>

      <div className="pt-4 border-t border-slate-700/50 flex items-center justify-between gap-2 mt-auto">
        {!device.isApproved ? (
          <div className="flex items-center justify-between w-full">
            <span className="text-xs font-bold text-warning uppercase tracking-wider flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-warning animate-pulse" />
              Pending
            </span>
            <button
              onClick={(e) => onApprove(e, device)}
              disabled={isApproving}
              className="bg-warning hover:bg-warning-600 text-slate-900 px-3 py-1.5 rounded-lg text-xs font-bold shadow-lg shadow-warning/20 transition-all active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isApproving ? "..." : "Approve"}
            </button>
          </div>
        ) : (
          <>
            <div className="flex items-center gap-1.5">
              <span className={`px-2 py-0.5 rounded text-[10px] font-bold uppercase border ${
                device.isOnline 
                  ? "bg-success/10 text-success border-success/20" 
                  : "bg-slate-700/50 text-slate-400 border-slate-600"
              }`}>
                {device.isOnline ? "Active" : "Offline"}
              </span>
            </div>
            <span className="text-xs font-medium text-primary-400 opacity-0 group-hover:opacity-100 transition-opacity flex items-center gap-1">
              Details →
            </span>
          </>
        )}
      </div>
    </div>
  );
}
