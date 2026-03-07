import { useState } from "react";
import type { DeviceDto } from "../types/device";
import { MoreVertical, Maximize2 } from "./Icons";

interface DeviceMonitorCardProps {
  device: DeviceDto;
  imageSrc?: string;
  onDoubleClick: (device: DeviceDto) => void;
}

export function DeviceMonitorCard({
  device,
  imageSrc,
  onDoubleClick,
}: DeviceMonitorCardProps) {
  const [isHovered, setIsHovered] = useState(false);

  return (
    <div
      className="relative group bg-slate-900 rounded-lg overflow-hidden border border-slate-800 hover:border-primary-500/50 transition-all shadow-sm hover:shadow-xl aspect-video cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onDoubleClick={() => onDoubleClick(device)}
    >
      {/* Monitor Screen Content */}
      <div className="absolute inset-0 flex items-center justify-center bg-black">
        {imageSrc ? (
          <img
            src={imageSrc}
            alt={`Screen of ${device.hostname}`}
            className="w-full h-full object-contain"
          />
        ) : (
          <div className="text-slate-700 flex flex-col items-center gap-2">
            <div className="w-12 h-12 rounded-full bg-slate-800 flex items-center justify-center">
              <span className="text-2xl">🖥️</span>
            </div>
            <span className="text-xs font-mono">NO SIGNAL</span>
          </div>
        )}
      </div>

      {/* Overlay - Always visible name strip at bottom, but subtle */}
      <div className="absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/90 via-black/60 to-transparent p-3 pt-8 flex justify-between items-end transition-opacity duration-300">
        <div className="flex items-center gap-2 min-w-0">
          <div
            className={`w-2 h-2 rounded-full ${device.isOnline ? "bg-success shadow-[0_0_8px_rgba(34,197,94,0.8)]" : "bg-red-500"}`}
            title={device.isOnline ? "Online" : "Offline"}
          />
          <span className="text-white font-medium text-sm truncate drop-shadow-md">
            {device.hostname}
          </span>
        </div>

        {/* Actions only on hover */}
        <div
          className={`flex gap-1 transition-opacity duration-200 ${isHovered ? "opacity-100" : "opacity-0"}`}
        >
          <button
            className="p-1.5 rounded-md hover:bg-white/10 text-white/80 hover:text-white transition-colors"
            title="Full Remote Control"
            onClick={(e) => {
              e.stopPropagation();
              onDoubleClick(device);
            }}
          >
            <Maximize2 className="w-4 h-4" />
          </button>
          <button
            className="p-1.5 rounded-md hover:bg-white/10 text-white/80 hover:text-white transition-colors"
            title="More Options"
          >
            <MoreVertical className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Hover Instruction Overlay (Central) */}
      {isHovered && (
        <div className="absolute inset-0 flex items-center justify-center pointer-events-none bg-black/10 backdrop-blur-[1px] transition-all duration-300">
          <div className="bg-black/60 px-3 py-1.5 rounded-full text-xs text-white/90 font-medium border border-white/10 shadow-lg transform translate-y-2">
            Double-click to Control
          </div>
        </div>
      )}
    </div>
  );
}
