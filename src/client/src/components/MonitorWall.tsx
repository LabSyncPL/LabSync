import { useState, useEffect } from "react";
import type { DeviceDto } from "../types/device";
import type { MonitorSettings } from "../hooks/useGridMonitor";
import { DeviceMonitorCard } from "./DeviceMonitorCard";
import {
  ArrowLeft,
  Maximize,
  Minimize,
  Pause,
  Play,
  Grid3X3,
  Grid2X2,
} from "./Icons";

const PRESETS: Record<string, MonitorSettings & { label: string }> = {
  low: { width: 400, quality: 50, fps: 1, label: "Low (BW Saver)" },
  medium: { width: 600, quality: 70, fps: 2, label: "Medium" },
  high: { width: 800, quality: 80, fps: 5, label: "High" },
  ultra: { width: 1280, quality: 85, fps: 10, label: "Ultra (High BW)" },
};

interface MonitorWallProps {
  devices: DeviceDto[];
  onBack: () => void;
  onDeviceDoubleClick: (device: DeviceDto) => void;
  isPaused: boolean;
  togglePause: () => void;
  images: Record<string, string>; // Base64 images from the hook
  currentSettings: MonitorSettings;
  onUpdateSettings: (settings: MonitorSettings) => void;
}

export function MonitorWall({
  devices,
  onBack,
  onDeviceDoubleClick,
  isPaused,
  togglePause,
  images,
  currentSettings,
  onUpdateSettings,
}: MonitorWallProps) {
  const [gridSize, setGridSize] = useState(3); // Columns
  const [isFullscreen, setIsFullscreen] = useState(false);

  const currentPresetKey =
    Object.entries(PRESETS).find(
      ([_, p]) =>
        p.width === currentSettings.width &&
        p.quality === currentSettings.quality &&
        p.fps === currentSettings.fps,
    )?.[0] || "custom";

  const handlePresetChange = (key: string) => {
    const preset = PRESETS[key];
    if (preset) {
      const { label, ...settings } = preset;
      onUpdateSettings(settings);
    }
  };
  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen();
      setIsFullscreen(true);
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen();
        setIsFullscreen(false);
      }
    }
  };

  useEffect(() => {
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        // If browser fullscreen is active, let browser handle it.
        // If not, we might want to exit immersive mode?
        // For now, let's keep it simple.
      }
    };
    window.addEventListener("keydown", handleEsc);
    return () => window.removeEventListener("keydown", handleEsc);
  }, []);

  return (
    <div className="fixed inset-0 z-50 bg-slate-950 flex flex-col overflow-hidden">
      {/* Top Bar - Sleek Glassmorphism Design */}
      <div className="bg-slate-950/60 backdrop-blur-xl border-b border-white/5 h-20 flex justify-between items-center px-6 z-20 shadow-2xl">
        {/* Left: Navigation & Status */}
        <div className="flex items-center gap-6">
          <button
            onClick={onBack}
            className="group flex items-center gap-3 px-4 py-2.5 bg-white/5 hover:bg-white/10 rounded-xl text-slate-300 hover:text-white transition-all duration-200 border border-white/5 hover:border-white/10"
          >
            <ArrowLeft className="w-5 h-5 text-slate-400 group-hover:text-white transition-colors" />
            <span className="font-medium text-sm tracking-wide">Exit Wall</span>
          </button>

          <div className="h-8 w-px bg-white/10" />

          <div className="flex flex-col">
            <div className="flex items-center gap-2.5">
              <span className={`relative flex h-2.5 w-2.5`}>
                <span
                  className={`animate-ping absolute inline-flex h-full w-full rounded-full opacity-75 ${isPaused ? "bg-warning" : "bg-emerald-500"}`}
                ></span>
                <span
                  className={`relative inline-flex rounded-full h-2.5 w-2.5 ${isPaused ? "bg-warning" : "bg-emerald-500"}`}
                ></span>
              </span>
              <span
                className={`text-sm font-semibold tracking-wide ${isPaused ? "text-slate-400" : "text-emerald-400"}`}
              >
                {isPaused ? "Monitoring Paused" : "Live Monitoring"}
              </span>
            </div>
            <div className="text-xs text-slate-500 font-medium mt-0.5 ml-5">
              {devices.length} active screens
            </div>
          </div>
        </div>

        {/* Right: Controls Toolbar */}
        <div className="flex items-center gap-4 bg-black/20 p-1.5 rounded-2xl border border-white/5 backdrop-blur-sm">
          {/* Grid Density Control */}
          <div className="flex items-center bg-slate-800/50 rounded-xl p-1 border border-white/5">
            <button
              onClick={() => setGridSize(Math.max(1, gridSize - 1))}
              className="p-2 hover:bg-white/10 rounded-lg text-slate-400 hover:text-white transition-all disabled:opacity-50"
              disabled={gridSize <= 1}
              title="Fewer Columns"
            >
              <Grid2X2 className="w-4 h-4" />
            </button>
            <div className="px-3 min-w-[3rem] text-center">
              <span className="block text-xs font-bold text-white">
                {gridSize}
              </span>
              <span className="block text-[10px] text-slate-500 uppercase tracking-wider">
                COLS
              </span>
            </div>
            <button
              onClick={() => setGridSize(Math.min(6, gridSize + 1))}
              className="p-2 hover:bg-white/10 rounded-lg text-slate-400 hover:text-white transition-all disabled:opacity-50"
              disabled={gridSize >= 6}
              title="More Columns"
            >
              <Grid3X3 className="w-4 h-4" />
            </button>
          </div>

          <div className="w-px h-8 bg-white/10 mx-1" />

          {/* Quality Selector */}
          <div className="flex items-center gap-3 px-2">
            <label className="text-xs text-slate-400 font-medium uppercase tracking-wider hidden sm:block">
              Quality
            </label>
            <div className="relative group">
              <select
                className="appearance-none bg-slate-800/80 hover:bg-slate-700 text-sm text-white font-medium pl-3 pr-8 py-2 rounded-lg border border-white/10 hover:border-primary-500/50 focus:outline-none focus:ring-2 focus:ring-primary-500/30 transition-all cursor-pointer min-w-[120px]"
                value={currentPresetKey}
                onChange={(e) => handlePresetChange(e.target.value)}
              >
                {Object.entries(PRESETS).map(([key, preset]) => (
                  <option
                    key={key}
                    value={key}
                    className="bg-slate-900 text-slate-300"
                  >
                    {preset.label}
                  </option>
                ))}
                {currentPresetKey === "custom" && (
                  <option
                    value="custom"
                    className="bg-slate-900 text-slate-300"
                  >
                    Custom
                  </option>
                )}
              </select>
              {/* Custom Chevron */}
              <div className="absolute right-2.5 top-1/2 -translate-y-1/2 pointer-events-none text-slate-400 group-hover:text-white transition-colors">
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
                    d="M19 9l-7 7-7-7"
                  ></path>
                </svg>
              </div>
            </div>
          </div>

          <div className="w-px h-8 bg-white/10 mx-1" />

          {/* Play/Pause Toggle */}
          <button
            onClick={togglePause}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl font-semibold text-sm transition-all shadow-lg ${
              isPaused
                ? "bg-emerald-600 hover:bg-emerald-500 text-white shadow-emerald-900/20"
                : "bg-amber-500/10 hover:bg-amber-500/20 text-amber-500 border border-amber-500/50"
            }`}
            title={isPaused ? "Resume Updates" : "Pause Updates"}
          >
            {isPaused ? (
              <>
                <Play className="w-4 h-4 fill-current" />
                <span>Resume</span>
              </>
            ) : (
              <>
                <Pause className="w-4 h-4 fill-current" />
                <span>Pause</span>
              </>
            )}
          </button>

          {/* Fullscreen Toggle */}
          <button
            onClick={toggleFullscreen}
            className="p-2.5 hover:bg-white/10 rounded-xl text-slate-400 hover:text-white transition-all border border-transparent hover:border-white/5 ml-1"
            title="Toggle Fullscreen"
          >
            {isFullscreen ? (
              <Minimize className="w-5 h-5" />
            ) : (
              <Maximize className="w-5 h-5" />
            )}
          </button>
        </div>
      </div>

      {/* Grid Content */}
      <div className="flex-1 overflow-y-auto p-6 bg-slate-950 custom-scrollbar">
        <div
          className="grid gap-6 transition-all duration-300 ease-in-out"
          style={{
            gridTemplateColumns: `repeat(${gridSize}, minmax(0, 1fr))`,
          }}
        >
          {devices.map((device) => (
            <div key={device.id} className="transition-all duration-300">
              <DeviceMonitorCard
                device={device}
                imageSrc={images[device.id]}
                onDoubleClick={onDeviceDoubleClick}
              />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
