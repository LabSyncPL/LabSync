import { useState } from "react";
import { useSystemMetricsSettings } from "../settings/systemMetricsSettings";
import { useRemoteDesktopSettings } from "../settings/remoteDesktopSettings";

type LocalMetricsSettings = {
  autoMode: "manual" | "auto" | "background";
  refreshIntervalSeconds: number;
  maxHistoryPoints: number;
};

type LocalRemoteDesktopSettings = {
  initialWidth: number;
  initialHeight: number;
  initialFps: number;
  initialBitrateKbps: number;
  preferredEncoder: string;
  autoResize: boolean;
};

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState<"metrics" | "remoteDesktop">(
    "remoteDesktop",
  );
  const [saveStatus, setSaveStatus] = useState<"idle" | "saving" | "saved">(
    "idle",
  );
  const [hasChanges, setHasChanges] = useState(false);

  // Metrics Settings
  const [storedMetrics, setStoredMetrics] = useSystemMetricsSettings();
  const [localMetrics, setLocalMetrics] = useState<LocalMetricsSettings>({
    autoMode: storedMetrics.autoMode,
    refreshIntervalSeconds: storedMetrics.refreshIntervalSeconds,
    maxHistoryPoints: storedMetrics.maxHistoryPoints,
  });

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

  const handleMetricsChange = (updates: Partial<LocalMetricsSettings>) => {
    setLocalMetrics((prev) => ({ ...prev, ...updates }));
    setHasChanges(true);
    setSaveStatus("idle");
  };

  const handleRemoteChange = (updates: Partial<LocalRemoteDesktopSettings>) => {
    setLocalRemote((prev) => ({ ...prev, ...updates }));
    setHasChanges(true);
    setSaveStatus("idle");
  };

  const saveSettings = () => {
    setSaveStatus("saving");

    // Save Metrics
    setStoredMetrics({
      autoMode: localMetrics.autoMode,
      refreshIntervalSeconds: localMetrics.refreshIntervalSeconds,
      maxHistoryPoints: localMetrics.maxHistoryPoints,
    });

    // Save Remote Desktop
    setStoredRemote({
      initialWidth: localRemote.initialWidth,
      initialHeight: localRemote.initialHeight,
      initialFps: localRemote.initialFps,
      initialBitrateKbps: localRemote.initialBitrateKbps,
      preferredEncoder: localRemote.preferredEncoder,
      autoResize: localRemote.autoResize,
    });

    setHasChanges(false);
    setSaveStatus("saved");
    setTimeout(() => setSaveStatus("idle"), 2000);
  };

  const currentMode = localMetrics.autoMode;

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
            <svg
              className={`w-4 h-4 mr-3 ${activeTab === "remoteDesktop" ? "text-primary-500" : ""}`}
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
              ></path>
            </svg>
            Remote Desktop
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-8 max-w-5xl">
          {activeTab === "metrics" && (
            <div className="space-y-10">
              <div className="mb-2">
                <div className="flex items-center justify-between mb-4">
                  <div>
                    <h2 className="text-lg font-medium text-white">
                      System metrics and monitoring
                    </h2>
                    <p className="text-xs text-slate-500">
                      Control how often agents collect metrics and how much
                      history the dashboard keeps.
                    </p>
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="bg-slate-800 border border-slate-700 rounded-lg p-5 space-y-4">
                    <h3 className="text-sm font-semibold text-white mb-1">
                      Collection mode
                    </h3>
                    <p className="text-xs text-slate-500 mb-3">
                      Choose whether metrics are collected only on demand, while
                      viewing a device, or continuously.
                    </p>

                    <div className="space-y-2">
                      <button
                        type="button"
                        onClick={() =>
                          handleMetricsChange({ autoMode: "manual" })
                        }
                        className={`w-full flex items-start px-3 py-2 rounded-lg border text-left text-xs ${
                          currentMode === "manual"
                            ? "border-primary-500 bg-primary-500/10 text-white"
                            : "border-slate-700 bg-slate-900/40 text-slate-300 hover:border-slate-500"
                        }`}
                      >
                        <span className="mt-0.5 mr-2">
                          <span
                            className={`w-3 h-3 inline-block rounded-full border ${
                              currentMode === "manual"
                                ? "border-primary-400 bg-primary-500"
                                : "border-slate-500"
                            }`}
                          ></span>
                        </span>
                        <span>
                          <span className="block font-semibold mb-0.5">
                            Manual only
                          </span>
                          <span className="block text-slate-400">
                            Metrics are collected only when you explicitly
                            trigger the action on a device.
                          </span>
                        </span>
                      </button>

                      <button
                        type="button"
                        onClick={() =>
                          handleMetricsChange({ autoMode: "auto" })
                        }
                        className={`w-full flex items-start px-3 py-2 rounded-lg border text-left text-xs ${
                          currentMode === "auto"
                            ? "border-primary-500 bg-primary-500/10 text-white"
                            : "border-slate-700 bg-slate-900/40 text-slate-300 hover:border-slate-500"
                        }`}
                      >
                        <span className="mt-0.5 mr-2">
                          <span
                            className={`w-3 h-3 inline-block rounded-full border ${
                              currentMode === "auto"
                                ? "border-primary-400 bg-primary-500"
                                : "border-slate-500"
                            }`}
                          ></span>
                        </span>
                        <span>
                          <span className="block font-semibold mb-0.5">
                            Automatic while viewing device
                          </span>
                          <span className="block text-slate-400">
                            Agents collect metrics periodically while the device
                            details page is open.
                          </span>
                        </span>
                      </button>

                      <button
                        type="button"
                        onClick={() =>
                          handleMetricsChange({ autoMode: "background" })
                        }
                        className={`w-full flex items-start px-3 py-2 rounded-lg border text-left text-xs ${
                          currentMode === "background"
                            ? "border-primary-500 bg-primary-500/10 text-white"
                            : "border-slate-700 bg-slate-900/40 text-slate-300 hover:border-slate-500"
                        }`}
                      >
                        <span className="mt-0.5 mr-2">
                          <span
                            className={`w-3 h-3 inline-block rounded-full border ${
                              currentMode === "background"
                                ? "border-primary-400 bg-primary-500"
                                : "border-slate-500"
                            }`}
                          ></span>
                        </span>
                        <span>
                          <span className="block font-semibold mb-0.5">
                            Continuous in background
                          </span>
                          <span className="block text-slate-400">
                            Agents collect metrics for all online devices at a
                            fixed interval while you are signed in.
                          </span>
                        </span>
                      </button>
                    </div>
                  </div>

                  <div className="bg-slate-800 border border-slate-700 rounded-lg p-5 space-y-4">
                    <h3 className="text-sm font-semibold text-white mb-1">
                      Sampling and history
                    </h3>
                    <p className="text-xs text-slate-500 mb-3">
                      Adjust how aggressive metric collection should be and how
                      much data the charts keep.
                    </p>

                    <div className="space-y-3">
                      <div>
                        <div className="flex items-center justify-between mb-1">
                          <label className="text-xs font-medium text-slate-300">
                            Refresh interval
                          </label>
                          <span className="text-xs text-slate-400">
                            {localMetrics.refreshIntervalSeconds} seconds
                          </span>
                        </div>
                        <select
                          className="w-full bg-slate-900 border border-slate-700 rounded-lg text-xs text-white px-3 py-2 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
                          value={localMetrics.refreshIntervalSeconds}
                          onChange={(e) =>
                            handleMetricsChange({
                              refreshIntervalSeconds: Number(e.target.value),
                            })
                          }
                        >
                          <option value={5}>Every 5 seconds</option>
                          <option value={10}>Every 10 seconds</option>
                          <option value={15}>Every 15 seconds</option>
                          <option value={30}>Every 30 seconds</option>
                          <option value={60}>Every 60 seconds</option>
                        </select>
                      </div>

                      <div>
                        <div className="flex items-center justify-between mb-1">
                          <label className="text-xs font-medium text-slate-300">
                            Chart history size
                          </label>
                          <span className="text-xs text-slate-400">
                            {localMetrics.maxHistoryPoints} samples
                          </span>
                        </div>
                        <input
                          type="range"
                          min={10}
                          max={600}
                          step={10}
                          value={localMetrics.maxHistoryPoints}
                          onChange={(e) =>
                            handleMetricsChange({
                              maxHistoryPoints: Number(e.target.value),
                            })
                          }
                          className="w-full accent-primary-500"
                        />
                        <div className="flex justify-between text-[10px] text-slate-500 mt-1">
                          <span>Short</span>
                          <span>Balanced</span>
                          <span>Long</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

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
        </div>
      </div>
    </>
  );
}
