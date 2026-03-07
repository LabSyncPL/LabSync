import { useEffect, useState } from "react";

export interface MonitorWallSettings {
  width: number;
  quality: number;
  fps: number;
}

const STORAGE_KEY = "labsync.monitorWallSettings.v1";
const SETTINGS_EVENT = "labsync.monitorWallSettings.change";

export const MONITOR_PRESETS: Record<
  string,
  MonitorSettings & { label: string }
> = {
  low: { width: 400, quality: 50, fps: 1, label: "Low (BW Saver)" },
  medium: { width: 600, quality: 70, fps: 2, label: "Medium" },
  high: { width: 800, quality: 80, fps: 5, label: "High" },
  ultra: { width: 1280, quality: 85, fps: 10, label: "Ultra (High BW)" },
};

export type MonitorSettings = {
  width: number;
  quality: number;
  fps: number;
};

export const defaultMonitorWallSettings: MonitorWallSettings =
  MONITOR_PRESETS.low;

function loadMonitorWallSettings(): MonitorWallSettings {
  if (typeof window === "undefined") {
    return defaultMonitorWallSettings;
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return defaultMonitorWallSettings;
    }
    const parsed = JSON.parse(raw) as Partial<MonitorWallSettings>;
    return {
      ...defaultMonitorWallSettings,
      ...parsed,
    };
  } catch {
    return defaultMonitorWallSettings;
  }
}

function saveMonitorWallSettings(settings: MonitorWallSettings): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
    window.dispatchEvent(new Event(SETTINGS_EVENT));
  } catch {
    void 0;
  }
}

export function useMonitorWallSettings(): [
  MonitorWallSettings,
  (next: MonitorWallSettings) => void,
] {
  const [settings, setSettings] = useState<MonitorWallSettings>(() =>
    loadMonitorWallSettings(),
  );

  const updateSettings = (next: MonitorWallSettings) => {
    setSettings(next);
    saveMonitorWallSettings(next);
  };

  useEffect(() => {
    const handleChange = () => {
      setSettings(loadMonitorWallSettings());
    };
    const storageHandler = (e: StorageEvent) => {
      if (e.key === STORAGE_KEY) {
        handleChange();
      }
    };
    window.addEventListener(SETTINGS_EVENT, handleChange);
    window.addEventListener("storage", storageHandler);
    return () => {
      window.removeEventListener(SETTINGS_EVENT, handleChange);
      window.removeEventListener("storage", storageHandler);
    };
  }, []);

  return [settings, updateSettings];
}
