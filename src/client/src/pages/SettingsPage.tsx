import { useState } from "react";
import { useSystemMetricsSettings } from "../settings/systemMetricsSettings";

type LocalMetricsSettings = {
  autoMode: "manual" | "auto" | "background";
  refreshIntervalSeconds: number;
  maxHistoryPoints: number;
};

export function SettingsPage() {
  const [storedSettings, setStoredSettings] = useSystemMetricsSettings();
  const [localSettings, setLocalSettings] = useState<LocalMetricsSettings>({
    autoMode: storedSettings.autoMode,
    refreshIntervalSeconds: storedSettings.refreshIntervalSeconds,
    maxHistoryPoints: storedSettings.maxHistoryPoints,
  });
  const [hasChanges, setHasChanges] = useState(false);
  const [justSaved, setJustSaved] = useState(false);

  const handleAutoModeChange = (mode: "manual" | "auto" | "background") => {
    setLocalSettings((prev) => ({
      ...prev,
      autoMode: mode,
    }));
    setHasChanges(true);
    setJustSaved(false);
  };

  const handleRefreshIntervalChange = (value: number) => {
    setLocalSettings((prev) => ({
      ...prev,
      refreshIntervalSeconds: value,
    }));
    setHasChanges(true);
    setJustSaved(false);
  };

  const handleMaxHistoryChange = (value: number) => {
    const clamped = Math.min(Math.max(value, 10), 600);
    setLocalSettings((prev) => ({
      ...prev,
      maxHistoryPoints: clamped,
    }));
    setHasChanges(true);
    setJustSaved(false);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const next = {
      autoMode: localSettings.autoMode,
      refreshIntervalSeconds: localSettings.refreshIntervalSeconds,
      maxHistoryPoints: localSettings.maxHistoryPoints,
    };
    setStoredSettings(next);
    setHasChanges(false);
    setJustSaved(true);
    window.setTimeout(() => setJustSaved(false), 2500);
  };

  const currentMode = localSettings.autoMode;

  return (
    <>
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <h1 className="text-xl font-bold text-white">Settings</h1>
        <button
          type="submit"
          form="settings-form"
          className="bg-primary-600 hover:bg-primary-500 text-white px-4 py-2 rounded-lg font-medium text-xs shadow-lg shadow-primary-500/20 disabled:opacity-50 disabled:cursor-not-allowed"
          disabled={!hasChanges}
        >
          Save configuration
        </button>
      </header>

      <div className="flex-1 flex overflow-hidden">
        <div className="w-64 bg-slate-950/50 border-r border-slate-800 p-6 space-y-1">
          <p className="px-2 text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">
            Services & Monitoring
          </p>

          <button className="w-full flex items-center px-3 py-2 text-white bg-slate-800 rounded-md border border-slate-700 shadow-sm">
            <svg
              className="w-4 h-4 mr-3 text-primary-500"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M12 6v6m0 0v6m0-6h6m-6 0H6"
              ></path>
            </svg>
            System metrics
          </button>

          <button className="w-full flex items-center px-3 py-2 text-slate-500 hover:text-white hover:bg-slate-800/50 rounded-md transition-colors">
            <svg
              className="w-4 h-4 mr-3"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"
              ></path>
            </svg>
            Other modules
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-8 max-w-5xl">
          <form id="settings-form" onSubmit={handleSubmit} className="space-y-10">
            <div className="mb-2">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h2 className="text-lg font-medium text-white">System metrics and monitoring</h2>
                  <p className="text-xs text-slate-500">
                    Control how often agents collect metrics and how much history the dashboard keeps.
                  </p>
                </div>
                {justSaved && (
                  <div className="px-3 py-1.5 rounded-full bg-success/10 border border-success/20 text-success text-xs font-medium">
                    Saved
                  </div>
                )}
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="bg-slate-800 border border-slate-700 rounded-lg p-5 space-y-4">
                  <h3 className="text-sm font-semibold text-white mb-1">Collection mode</h3>
                  <p className="text-xs text-slate-500 mb-3">
                    Choose whether metrics are collected only on demand, while viewing a device, or continuously.
                  </p>

                  <div className="space-y-2">
                    <button
                      type="button"
                      onClick={() => handleAutoModeChange("manual")}
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
                        <span className="block font-semibold mb-0.5">Manual only</span>
                        <span className="block text-slate-400">
                          Metrics are collected only when you explicitly trigger the action on a device.
                        </span>
                      </span>
                    </button>

                    <button
                      type="button"
                      onClick={() => handleAutoModeChange("auto")}
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
                        <span className="block font-semibold mb-0.5">Automatic while viewing device</span>
                        <span className="block text-slate-400">
                          Agents collect metrics periodically while the device details page is open.
                        </span>
                      </span>
                    </button>

                    <button
                      type="button"
                      onClick={() => handleAutoModeChange("background")}
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
                        <span className="block font-semibold mb-0.5">Continuous in background</span>
                        <span className="block text-slate-400">
                          Agents collect metrics for all online devices at a fixed interval while you are signed in.
                        </span>
                      </span>
                    </button>
                  </div>
                </div>

                <div className="bg-slate-800 border border-slate-700 rounded-lg p-5 space-y-4">
                  <h3 className="text-sm font-semibold text-white mb-1">Sampling and history</h3>
                  <p className="text-xs text-slate-500 mb-3">
                    Adjust how aggressive metric collection should be and how much data the charts keep.
                  </p>

                  <div className="space-y-3">
                    <div>
                      <div className="flex items-center justify-between mb-1">
                        <label className="text-xs font-medium text-slate-300">
                          Refresh interval
                        </label>
                        <span className="text-xs text-slate-400">
                          {localSettings.refreshIntervalSeconds} seconds
                        </span>
                      </div>
                      <select
                        className="w-full bg-slate-900 border border-slate-700 rounded-lg text-xs text-white px-3 py-2 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
                        value={localSettings.refreshIntervalSeconds}
                        onChange={(e) => handleRefreshIntervalChange(Number(e.target.value))}
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
                          {localSettings.maxHistoryPoints} samples
                        </span>
                      </div>
                      <input
                        type="range"
                        min={10}
                        max={600}
                        step={10}
                        value={localSettings.maxHistoryPoints}
                        onChange={(e) => handleMaxHistoryChange(Number(e.target.value))}
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
          </form>
        </div>
      </div>
    </>
  );
}
