import { useState } from "react";
import { useRemoteDesktopSettings } from "../settings/remoteDesktopSettings";
import {
  useMonitorWallSettings,
  MONITOR_PRESETS,
} from "../settings/monitorWallSettings";

type LocalRemoteDesktopSettings = {
  initialWidth: number;
  initialHeight: number;
  initialFps: number;
  initialBitrateKbps: number;
  preferredEncoder: string;
  autoResize: boolean;
};

type LocalMonitorWallSettings = {
  preset: string;
};

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState<"remoteDesktop" | "monitorWall">(
    "remoteDesktop",
  );
  const [saveStatus, setSaveStatus] = useState<"idle" | "saving" | "saved">(
    "idle",
  );
  const [hasChanges, setHasChanges] = useState(false);

  // Remote Desktop Settings
  const [storedRemote, setStoredRemote] = useRemoteDesktopSettings();
  const [localRemote, setLocalRemote] = useState<LocalRemoteDesktopSettings>({
    initialWidth: storedRemote.initialWidth,
    initialHeight: storedRemote.initialHeight,
    initialFps: storedRemote.initialFps,
    initialBitrateKbps: storedRemote.initialBitrateKbps,
    preferredEncoder: storedRemote.preferredEncoder,
    autoResize: storedRemote.autoResize,
  });

  // Monitor Wall Settings
  const [storedMonitor, setStoredMonitor] = useMonitorWallSettings();

  // Find matching preset or default to low
  const getPresetKey = (settings: typeof storedMonitor) => {
    return (
      Object.entries(MONITOR_PRESETS).find(
        ([_, p]) =>
          p.width === settings.width &&
          p.quality === settings.quality &&
          p.fps === settings.fps,
      )?.[0] || "low"
    );
  };

  const [localMonitor, setLocalMonitor] = useState<LocalMonitorWallSettings>({
    preset: getPresetKey(storedMonitor),
  });

  const handleRemoteChange = (updates: Partial<LocalRemoteDesktopSettings>) => {
    setLocalRemote((prev) => ({ ...prev, ...updates }));
    setHasChanges(true);
    setSaveStatus("idle");
  };

  const handleMonitorChange = (updates: Partial<LocalMonitorWallSettings>) => {
    setLocalMonitor((prev) => ({ ...prev, ...updates }));
    setHasChanges(true);
    setSaveStatus("idle");
  };

  const saveSettings = () => {
    setSaveStatus("saving");

    // Save Remote Desktop
    setStoredRemote({
      initialWidth: localRemote.initialWidth,
      initialHeight: localRemote.initialHeight,
      initialFps: localRemote.initialFps,
      initialBitrateKbps: localRemote.initialBitrateKbps,
      preferredEncoder: localRemote.preferredEncoder,
      autoResize: localRemote.autoResize,
    });

    // Save Monitor Wall
    const selectedPreset = MONITOR_PRESETS[localMonitor.preset];
    if (selectedPreset) {
      setStoredMonitor({
        width: selectedPreset.width,
        quality: selectedPreset.quality,
        fps: selectedPreset.fps,
      });
    }

    setHasChanges(false);
    setSaveStatus("saved");
    setTimeout(() => setSaveStatus("idle"), 2000);
  };

  return (
    <>
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <h1 className="text-xl font-bold text-white">Settings</h1>
        <div className="flex items-center gap-4">
          {saveStatus === "saved" && (
            <span className="text-green-500 text-sm font-medium">
              Settings saved!
            </span>
          )}
          <button
            onClick={saveSettings}
            className={`bg-primary-600 hover:bg-primary-500 text-white px-4 py-2 rounded-lg font-medium text-xs shadow-lg shadow-primary-500/20 transition-all ${!hasChanges ? "opacity-50 cursor-not-allowed" : ""}`}
            disabled={!hasChanges}
          >
            Save configuration
          </button>
        </div>
      </header>

      <div className="flex-1 flex overflow-hidden">
        <div className="w-64 bg-slate-950/50 border-r border-slate-800 p-6 space-y-1">
          <p className="px-2 text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">
            Configuration
          </p>

          <button
            onClick={() => setActiveTab("remoteDesktop")}
            className={`w-full flex items-center px-3 py-2 rounded-md transition-colors text-sm ${activeTab === "remoteDesktop" ? "bg-slate-800 text-white border border-slate-700 shadow-sm" : "text-slate-500 hover:text-white hover:bg-slate-800/50"}`}
          >
            <span className="w-2 h-2 rounded-full bg-blue-500 mr-2"></span>
            Remote Desktop
          </button>

          <button
            onClick={() => setActiveTab("monitorWall")}
            className={`w-full flex items-center px-3 py-2 rounded-md transition-colors text-sm ${activeTab === "monitorWall" ? "bg-slate-800 text-white border border-slate-700 shadow-sm" : "text-slate-500 hover:text-white hover:bg-slate-800/50"}`}
          >
            <span className="w-2 h-2 rounded-full bg-purple-500 mr-2"></span>
            Monitor Wall
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-8 max-w-5xl">
          {activeTab === "remoteDesktop" && (
            <div className="space-y-10">
              <div className="mb-2">
                <div className="flex items-center justify-between mb-4">
                  <div>
                    <h2 className="text-lg font-medium text-white">
                      Remote Desktop Defaults
                    </h2>
                    <p className="text-xs text-slate-500">
                      Set your preferred default settings for new remote desktop
                      sessions.
                    </p>
                  </div>
                </div>

                <div className="bg-slate-800 border border-slate-700 rounded-lg p-5 space-y-6">
                  <div className="flex items-center justify-between p-4 bg-slate-900/50 rounded-lg border border-slate-700/50">
                    <div>
                      <h3 className="text-sm font-semibold text-white">
                        Auto-Resize
                      </h3>
                      <p className="text-xs text-slate-500">
                        Automatically adjust remote resolution to fit your local
                        window.
                      </p>
                    </div>
                    <div className="relative inline-block w-10 h-5 transition duration-200 ease-in-out">
                      <input
                        type="checkbox"
                        id="toggle-autoresize"
                        className="peer absolute opacity-0 w-0 h-0"
                        checked={localRemote.autoResize}
                        onChange={(e) =>
                          handleRemoteChange({ autoResize: e.target.checked })
                        }
                      />
                      <label
                        htmlFor="toggle-autoresize"
                        className={`block w-10 h-5 rounded-full cursor-pointer transition-colors duration-200 ${localRemote.autoResize ? "bg-primary-600" : "bg-slate-700"}`}
                      ></label>
                      <div
                        className={`absolute left-1 top-1 bg-white w-3 h-3 rounded-full transition-transform duration-200 ${localRemote.autoResize ? "translate-x-5" : "translate-x-0"}`}
                      ></div>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                      <label className="block text-xs font-medium text-slate-400">
                        Preferred Encoder
                      </label>
                      <select
                        value={localRemote.preferredEncoder}
                        onChange={(e) =>
                          handleRemoteChange({
                            preferredEncoder: e.target.value,
                          })
                        }
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-primary-500"
                      >
                        <option value="Auto">Auto (Best Available)</option>
                        <option value="Software">Software (CPU)</option>
                        <option value="NvidiaNvenc">NVIDIA NVENC</option>
                        <option value="AmdAmf">AMD AMF</option>
                        <option value="IntelQsv">Intel QSV</option>
                      </select>
                      <p className="text-[10px] text-slate-500">
                        The agent will try to use this encoder if available.
                        Auto will select the best GPU encoder.
                      </p>
                    </div>

                    <div className="space-y-2">
                      <label className="block text-xs font-medium text-slate-400">
                        Target FPS
                      </label>
                      <select
                        value={localRemote.initialFps}
                        onChange={(e) =>
                          handleRemoteChange({
                            initialFps: parseInt(e.target.value),
                          })
                        }
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-primary-500"
                      >
                        <option value="15">15 FPS</option>
                        <option value="30">30 FPS</option>
                        <option value="60">60 FPS</option>
                      </select>
                    </div>

                    <div className="space-y-2">
                      <label className="block text-xs font-medium text-slate-400">
                        Bitrate
                      </label>
                      <select
                        value={localRemote.initialBitrateKbps}
                        onChange={(e) =>
                          handleRemoteChange({
                            initialBitrateKbps:
                              parseInt(e.target.value) || 4000,
                          })
                        }
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-primary-500"
                      >
                        <option value="1000">1 Mbps (Low)</option>
                        <option value="2000">2 Mbps (Medium)</option>
                        <option value="4000">4 Mbps (High)</option>
                        <option value="8000">8 Mbps (Ultra)</option>
                        <option value="16000">16 Mbps (Lossless-ish)</option>
                      </select>
                    </div>

                    <div className="space-y-2">
                      <label className="block text-xs font-medium text-slate-400">
                        Default Resolution
                      </label>
                      <div className="grid grid-cols-2 gap-2">
                        <input
                          type="number"
                          placeholder="Width"
                          value={localRemote.initialWidth}
                          onChange={(e) =>
                            handleRemoteChange({
                              initialWidth: parseInt(e.target.value) || 1920,
                            })
                          }
                          className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-primary-500 disabled:opacity-50"
                          disabled={localRemote.autoResize}
                        />
                        <input
                          type="number"
                          placeholder="Height"
                          value={localRemote.initialHeight}
                          onChange={(e) =>
                            handleRemoteChange({
                              initialHeight: parseInt(e.target.value) || 1080,
                            })
                          }
                          className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-primary-500 disabled:opacity-50"
                          disabled={localRemote.autoResize}
                        />
                      </div>
                      {localRemote.autoResize && (
                        <p className="text-[10px] text-amber-500">
                          Managed automatically when Auto-Resize is enabled.
                        </p>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activeTab === "monitorWall" && (
            <div className="space-y-10">
              <div className="mb-2">
                <div className="flex items-center justify-between mb-4">
                  <div>
                    <h2 className="text-lg font-medium text-white">
                      Monitor Wall Defaults
                    </h2>
                    <p className="text-xs text-slate-500">
                      Set your preferred default settings for the Monitor Wall
                      (grid view).
                    </p>
                  </div>
                </div>

                <div className="bg-slate-800 border border-slate-700 rounded-lg p-5 space-y-6">
                  <div className="grid grid-cols-1 gap-6">
                    <div className="space-y-2">
                      <label className="block text-xs font-medium text-slate-400">
                        Performance Preset
                      </label>
                      <select
                        value={localMonitor.preset}
                        onChange={(e) =>
                          handleMonitorChange({
                            preset: e.target.value,
                          })
                        }
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-primary-500"
                      >
                        {Object.entries(MONITOR_PRESETS).map(
                          ([key, preset]) => (
                            <option key={key} value={key}>
                              {preset.label} ({preset.width}px, {preset.fps}{" "}
                              FPS)
                            </option>
                          ),
                        )}
                      </select>
                      <p className="text-[10px] text-slate-500">
                        Select a quality preset for the grid view. Higher
                        quality requires more bandwidth.
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
