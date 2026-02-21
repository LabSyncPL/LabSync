import { useEffect, useState } from "react";

export type MetricsAutoMode = "manual" | "auto" | "background";

export interface SystemMetricsSettings {
  autoMode: MetricsAutoMode;
  refreshIntervalSeconds: number;
  maxHistoryPoints: number;
}

const STORAGE_KEY = "labsync.systemMetricsSettings.v1";
const SETTINGS_EVENT = "labsync.systemMetricsSettings.change";

function getDefaultSystemMetricsSettings(): SystemMetricsSettings {
  return {
    autoMode: "auto",
    refreshIntervalSeconds: 15,
    maxHistoryPoints: 60,
  };
}

function loadSystemMetricsSettings(): SystemMetricsSettings {
  if (typeof window === "undefined") {
    return getDefaultSystemMetricsSettings();
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return getDefaultSystemMetricsSettings();
    }
    const parsed = JSON.parse(raw) as Partial<SystemMetricsSettings>;
    const defaults = getDefaultSystemMetricsSettings();
    return {
      autoMode:
        parsed.autoMode === "manual" ||
        parsed.autoMode === "auto" ||
        parsed.autoMode === "background"
          ? parsed.autoMode
          : defaults.autoMode,
      refreshIntervalSeconds:
        typeof parsed.refreshIntervalSeconds === "number" &&
        parsed.refreshIntervalSeconds > 0
          ? parsed.refreshIntervalSeconds
          : defaults.refreshIntervalSeconds,
      maxHistoryPoints:
        typeof parsed.maxHistoryPoints === "number" &&
        parsed.maxHistoryPoints > 0
          ? parsed.maxHistoryPoints
          : defaults.maxHistoryPoints,
    };
  } catch {
    return getDefaultSystemMetricsSettings();
  }
}

function saveSystemMetricsSettings(settings: SystemMetricsSettings): void {
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

export function useSystemMetricsSettings(): [
  SystemMetricsSettings,
  (next: SystemMetricsSettings) => void,
] {
  const [settings, setSettings] = useState<SystemMetricsSettings>(() =>
    loadSystemMetricsSettings(),
  );

  const updateSettings = (next: SystemMetricsSettings) => {
    setSettings(next);
    saveSystemMetricsSettings(next);
  };

  useEffect(() => {
    const handleChange = () => {
      setSettings(loadSystemMetricsSettings());
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
