import { useEffect, useState } from "react";

export interface RemoteDesktopSettings {
  initialWidth: number;
  initialHeight: number;
  initialFps: number;
  initialBitrateKbps: number;
  preferredEncoder: string;
  autoResize: boolean;
}

const STORAGE_KEY = "labsync.remoteDesktopSettings.v1";
const SETTINGS_EVENT = "labsync.remoteDesktopSettings.change";

export const defaultRemoteDesktopSettings: RemoteDesktopSettings = {
  initialWidth: 1920,
  initialHeight: 1080,
  initialFps: 30,
  initialBitrateKbps: 4000,
  preferredEncoder: "Auto",
  autoResize: true,
};

function loadRemoteDesktopSettings(): RemoteDesktopSettings {
  if (typeof window === "undefined") {
    return defaultRemoteDesktopSettings;
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return defaultRemoteDesktopSettings;
    }
    const parsed = JSON.parse(raw) as Partial<RemoteDesktopSettings>;
    return {
      ...defaultRemoteDesktopSettings,
      ...parsed,
    };
  } catch {
    return defaultRemoteDesktopSettings;
  }
}

function saveRemoteDesktopSettings(settings: RemoteDesktopSettings): void {
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

export function useRemoteDesktopSettings(): [
  RemoteDesktopSettings,
  (next: RemoteDesktopSettings) => void,
] {
  const [settings, setSettings] = useState<RemoteDesktopSettings>(() =>
    loadRemoteDesktopSettings(),
  );

  const updateSettings = (next: RemoteDesktopSettings) => {
    setSettings(next);
    saveRemoteDesktopSettings(next);
  };

  useEffect(() => {
    const handleChange = () => {
      setSettings(loadRemoteDesktopSettings());
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
