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
      {/* Top Bar (Auto-hides or minimal) */}
      <div className="bg-slate-900/80 backdrop-blur-md border-b border-slate-800 p-4 flex justify-between items-center z-10 shadow-lg">
        <div className="flex items-center gap-4">
          <button
            onClick={onBack}
            className="p-2 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-white transition-colors flex items-center gap-2"
          >
            <ArrowLeft className="w-5 h-5" />
            <span className="font-medium">Exit Wall</span>
          </button>

          <div className="h-6 w-px bg-slate-700 mx-2" />

          <div className="flex items-center gap-2 text-sm text-slate-400">
            <span className="w-2 h-2 rounded-full bg-success animate-pulse" />
            {isPaused ? "Monitoring Paused" : "Live Monitoring"}
            <span className="bg-slate-800 px-2 py-0.5 rounded text-xs border border-slate-700">
              {devices.length} screens
            </span>
          </div>
        </div>

        {/* Toolbar Controls */}
        <div className="flex items-center gap-4">
          {/* Zoom / Grid Size */}
          <div className="flex items-center gap-2 bg-slate-800 rounded-lg p-1 border border-slate-700">
            <button
              onClick={() => setGridSize(Math.max(1, gridSize - 1))}
              className="p-1.5 hover:bg-slate-700 rounded text-slate-400 hover:text-white"
              title="Larger Tiles"
            >
              <Grid2X2 className="w-4 h-4" />
            </button>
            <span className="text-xs w-8 text-center font-mono text-slate-400">
              {gridSize} col
            </span>
            <button
              onClick={() => setGridSize(Math.min(6, gridSize + 1))}
              className="p-1.5 hover:bg-slate-700 rounded text-slate-400 hover:text-white"
              title="Smaller Tiles"
            >
              <Grid3X3 className="w-4 h-4" />
            </button>
          </div>

          <div className="h-6 w-px bg-slate-700" />

          {/* Quality Selector */}
          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-400 font-medium uppercase tracking-wider">
              Quality
            </span>
            <select
              className="bg-slate-800 text-xs text-slate-300 border border-slate-700 rounded px-2 py-1.5 focus:outline-none focus:border-primary-500 transition-colors cursor-pointer"
              value={currentPresetKey}
              onChange={(e) => handlePresetChange(e.target.value)}
            >
              {Object.entries(PRESETS).map(([key, preset]) => (
                <option key={key} value={key}>
                  {preset.label}
                </option>
              ))}
              {currentPresetKey === "custom" && (
                <option value="custom">Custom</option>
              )}
            </select>
          </div>

          <div className="h-6 w-px bg-slate-700" />

          <button
            onClick={togglePause}
            className={`p-2 rounded-lg transition-all ${
              isPaused
                ? "bg-warning/20 text-warning hover:bg-warning/30"
                : "bg-slate-800 text-slate-300 hover:bg-slate-700 hover:text-white"
            }`}
            title={isPaused ? "Resume Updates" : "Pause Updates"}
          >
            {isPaused ? (
              <Play className="w-5 h-5 fill-current" />
            ) : (
              <Pause className="w-5 h-5 fill-current" />
            )}
          </button>

          <button
            onClick={toggleFullscreen}
            className="p-2 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-white transition-colors"
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
