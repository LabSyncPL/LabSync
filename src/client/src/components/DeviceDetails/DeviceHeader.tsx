import { useState } from "react";
import { useNavigate } from "react-router-dom";
import type { DeviceDto } from "../../types/device";
import { DEVICE_PLATFORM_LABELS } from "../../types/device";
import { getPlatformIcon } from "../../utils/deviceUtils";
import { RemoteControlModal } from "../RemoteControl/RemoteControlModal";

interface DeviceHeaderProps {
  device: DeviceDto;
  onOpenTerminal?: () => void;
  onConfigureCredentials?: () => void;
}

export function DeviceHeader({
  device,
  onOpenTerminal,
  onConfigureCredentials,
}: DeviceHeaderProps) {
  const navigate = useNavigate();
  const [showRemoteControl, setShowRemoteControl] = useState(false);

  return (
    <>
      <header className="bg-slate-900 border-b border-slate-800 pb-6 pt-4 px-8 shrink-0">
        <div className="flex items-center text-xs text-slate-500 mb-6">
          <button
            onClick={() => navigate("/")}
            className="hover:text-slate-300 transition-colors font-medium"
          >
            Dashboard
          </button>
          <svg
            className="w-3 h-3 mx-2 text-slate-600"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              d="M9 5l7 7-7 7"
            />
          </svg>
          <span className="text-slate-300 font-medium bg-slate-800 px-2 py-0.5 rounded">
            Device Details
          </span>
        </div>

        <div className="flex flex-col md:flex-row md:items-start justify-between gap-6">
          <div className="flex items-start gap-5">
            <div className="w-16 h-16 rounded-2xl bg-slate-800 border border-slate-700 flex items-center justify-center text-slate-400 shrink-0 shadow-xl shadow-black/20">
              {getPlatformIcon(device.platform, "w-8 h-8")}
            </div>
            <div>
              <h1 className="text-3xl font-bold text-white tracking-tight leading-none mb-3">
                {device.hostname}
              </h1>
              <div className="flex items-center gap-3 text-sm">
                <span className="text-slate-400 font-medium px-2.5 py-0.5 bg-slate-800 rounded border border-slate-700">
                  {DEVICE_PLATFORM_LABELS[device.platform]}
                </span>

                {device.isOnline ? (
                  <span className="inline-flex items-center text-success font-medium px-2.5 py-0.5 bg-success/10 rounded border border-success/20">
                    <span className="w-2 h-2 rounded-full bg-success mr-2 animate-pulse"></span>
                    Online
                  </span>
                ) : (
                  <span className="inline-flex items-center text-slate-400 font-medium px-2.5 py-0.5 bg-slate-800 rounded border border-slate-600">
                    <span className="w-2 h-2 rounded-full bg-slate-500 mr-2"></span>
                    Offline
                  </span>
                )}

                {!device.isApproved && (
                  <span className="inline-flex items-center text-warning font-medium px-2.5 py-0.5 bg-warning/10 rounded border border-warning/20">
                    <span className="w-2 h-2 rounded-full bg-warning mr-2"></span>
                    Pending Approval
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {onConfigureCredentials && device.hasSshCredentials && (
              <button
                onClick={onConfigureCredentials}
                className="px-4 py-2.5 rounded-lg bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-300 text-sm font-medium transition-all flex items-center gap-2 hover:text-white"
                title="Update SSH Credentials"
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
                    d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"
                  />
                </svg>
              </button>
            )}
            {onOpenTerminal && (
              <button
                onClick={onOpenTerminal}
                className="px-4 py-2.5 rounded-lg bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-300 text-sm font-medium transition-all flex items-center gap-2 hover:text-white"
              >
                Open SSH Terminal
              </button>
            )}
            <button className="px-4 py-2.5 rounded-lg bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-300 text-sm font-medium transition-all flex items-center gap-2 hover:text-white">
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
                  d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
                />
              </svg>
              Restart Agent
            </button>
            <button
              onClick={() => setShowRemoteControl(true)}
              disabled={!device.isOnline}
              className="px-5 py-2.5 rounded-lg bg-primary-600 hover:bg-primary-500 text-white text-sm font-semibold shadow-lg shadow-primary-500/20 transition-all flex items-center gap-2 hover:translate-y-[-1px] disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:translate-y-0"
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
                  d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z"
                />
              </svg>
              Remote View
            </button>
          </div>
        </div>
      </header>

      {showRemoteControl && (
        <RemoteControlModal
          deviceId={device.id}
          onClose={() => setShowRemoteControl(false)}
        />
      )}
    </>
  );
}
