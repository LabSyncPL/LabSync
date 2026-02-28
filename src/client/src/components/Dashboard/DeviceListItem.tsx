import { useNavigate } from "react-router-dom";
import type { DeviceDto } from "../../types/device";
import { DEVICE_PLATFORM_LABELS } from "../../types/device";
import { formatLastSeen, getPlatformIcon } from "../../utils/deviceUtils";

interface DeviceListItemProps {
  device: DeviceDto;
  onApprove: (e: React.MouseEvent, device: DeviceDto) => void;
  isApproving: boolean;
}

export function DeviceListItem({
  device,
  onApprove,
  isApproving,
}: DeviceListItemProps) {
  const navigate = useNavigate();

  return (
    <div
      onClick={() => navigate(`/devices/${device.id}`)}
      className={`group bg-slate-800 rounded-xl border border-slate-700 p-4 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-700/30 hover:border-slate-600 transition-all active:scale-[0.99] relative overflow-hidden ${
        !device.isApproved ? "border-l-4 border-l-warning" : ""
      }`}
    >
      <div className="flex items-center gap-4 min-w-[280px]">
        <div
          className={`w-10 h-10 rounded-lg flex items-center justify-center text-slate-400 border border-slate-600/30 group-hover:bg-slate-700 group-hover:text-white transition-colors ${
            device.isOnline
              ? "bg-success/5 text-success border-success/20"
              : "bg-slate-700/50"
          }`}
        >
          {getPlatformIcon(device.platform)}
        </div>
        <div>
          <h3 className="font-bold text-white text-sm truncate group-hover:text-primary-400 transition-colors flex items-center gap-2">
            {device.hostname}
            {!device.isApproved && (
              <span
                className="w-2 h-2 rounded-full bg-warning animate-pulse"
                title="Pending Approval"
              />
            )}
          </h3>
          <div className="flex items-center gap-2 mt-0.5">
            {device.groupName ? (
              <span className="text-[10px] font-bold uppercase bg-slate-700 text-slate-300 px-1.5 py-0.5 rounded border border-slate-600">
                {device.groupName}
              </span>
            ) : (
              <span className="text-[10px] font-medium text-slate-600 uppercase border border-dashed border-slate-700 px-1.5 py-0.5 rounded">
                No Group
              </span>
            )}
            <span className="text-slate-600 text-[10px]">•</span>
            <span className="text-xs text-slate-500 font-medium truncate">
              {DEVICE_PLATFORM_LABELS[device.platform]}
            </span>
          </div>
        </div>
      </div>

      <div className="hidden lg:flex items-center gap-6 flex-1 justify-center text-xs text-slate-400 font-mono">
        <div className="flex flex-col items-center">
          <span className="text-[10px] text-slate-500 uppercase tracking-wider mb-0.5">
            IP Address
          </span>
          <span className="bg-slate-900/50 px-2 py-0.5 rounded border border-slate-800 text-slate-300">
            {device.ipAddress ?? "—"}
          </span>
        </div>
        <div className="w-px h-6 bg-slate-700/50"></div>
        <div className="flex flex-col items-center">
          <span className="text-[10px] text-slate-500 uppercase tracking-wider mb-0.5">
            Last Seen
          </span>
          <span className="text-slate-300">
            {formatLastSeen(device.lastSeenAt)}
          </span>
        </div>
      </div>

      <div className="flex items-center gap-3 justify-end min-w-[160px]">
        {!device.isApproved ? (
          <button
            onClick={(e) => {
              e.stopPropagation();
              onApprove(e, device);
            }}
            disabled={isApproving}
            className="bg-warning hover:bg-warning-600 text-slate-900 px-3 py-1.5 rounded-lg text-xs font-bold shadow-sm transition-all active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isApproving ? (
              <svg
                className="w-3 h-3 animate-spin"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                />
              </svg>
            ) : (
              <svg
                className="w-3.5 h-3.5"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M5 13l4 4L19 7"
                />
              </svg>
            )}
            Approve
          </button>
        ) : (
          <>
            <span
              className={`px-2 py-0.5 rounded text-[10px] font-bold uppercase border ${
                device.isOnline
                  ? "bg-success/10 text-success border-success/20"
                  : "bg-slate-700/50 text-slate-400 border-slate-600"
              }`}
            >
              {device.isOnline ? "Active" : "Offline"}
            </span>

            <button
              className="p-1.5 text-slate-500 hover:text-white hover:bg-slate-700 rounded transition-colors ml-1"
              onClick={(e) => e.stopPropagation()}
              title="Manage Group"
            >
              <svg
                className="w-4 h-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z"
                />
              </svg>
            </button>
          </>
        )}
      </div>
    </div>
  );
}
