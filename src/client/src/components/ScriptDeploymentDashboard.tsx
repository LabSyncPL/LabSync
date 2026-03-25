import { useMemo, useRef, useState } from "react";
import Editor from "@monaco-editor/react";
import { useQuery } from "@tanstack/react-query";
import { devicesQueryKey, fetchDevices } from "../api/devices";
import type { DeviceDto } from "../types/device";
import {
  type ScriptInterpreter,
  type ScriptExecutionStatus,
  useMultiDeviceScriptRunner,
} from "../hooks/useMultiDeviceScriptRunner";

const DEFAULT_SCRIPT = [
  "# Write your deployment script here",
  "",
].join("\n");

const STATUS_BADGE_CLASS: Record<ScriptExecutionStatus, string> = {
  pending: "bg-slate-700 text-slate-200",
  running: "bg-blue-600/20 text-blue-300",
  success: "bg-emerald-600/20 text-emerald-300",
  error: "bg-rose-600/20 text-rose-300",
  timeout: "bg-amber-600/20 text-amber-300",
};

const STATUS_LABEL: Record<ScriptExecutionStatus, string> = {
  pending: "Pending",
  running: "Running",
  success: "Success",
  error: "Error",
  timeout: "Timeout",
};

const monacoLanguage = (interpreter: ScriptInterpreter) =>
  interpreter === "powershell" ? "powershell" : "shell";

const inferInterpreterFromFileName = (name: string): ScriptInterpreter | null => {
  const lower = name.toLowerCase();
  if (lower.endsWith(".ps1")) return "powershell";
  if (lower.endsWith(".sh")) return "bash";
  return null;
};

const buildDeviceGroups = (devices: DeviceDto[]) => {
  const map = new Map<string, { id: string; name: string; deviceIds: string[] }>();
  for (const device of devices) {
    const groupId = device.groupId || "__ungrouped__";
    const groupName = device.groupName || "Ungrouped";
    if (!map.has(groupId)) {
      map.set(groupId, { id: groupId, name: groupName, deviceIds: [] });
    }
    map.get(groupId)!.deviceIds.push(device.id);
  }
  return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name));
};

export function ScriptDeploymentDashboard() {
  const [scriptContent, setScriptContent] = useState(DEFAULT_SCRIPT);
  const [interpreter, setInterpreter] = useState<ScriptInterpreter>("powershell");
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<string[]>([]);
  const [activeLogRowKey, setActiveLogRowKey] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const devicesQuery = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
  });

  const {
    rows,
    connectionState,
    connectionError,
    lastInvokeError,
    runOnDevices,
    cancelMachine,
    stopAll,
    clearFinished,
  } = useMultiDeviceScriptRunner();

  const devices = devicesQuery.data || [];
  const groups = useMemo(() => buildDeviceGroups(devices), [devices]);
  const selectedIdSet = useMemo(() => new Set(selectedDeviceIds), [selectedDeviceIds]);
  const machineNamesById = useMemo(() => {
    const map: Record<string, string> = {};
    devices.forEach((d) => {
      map[d.id] = d.hostname;
    });
    return map;
  }, [devices]);

  const activeLogRow = useMemo(() => {
    if (!activeLogRowKey) return null;
    return rows.find((row) => `${row.taskId}::${row.machineId}` === activeLogRowKey) ?? null;
  }, [activeLogRowKey, rows]);

  const toggleDevice = (id: string) => {
    setSelectedDeviceIds((prev) =>
      prev.includes(id) ? prev.filter((value) => value !== id) : [...prev, id],
    );
  };

  const toggleGroup = (ids: string[]) => {
    setSelectedDeviceIds((prev) => {
      const prevSet = new Set(prev);
      const allSelected = ids.every((id) => prevSet.has(id));
      if (allSelected) {
        ids.forEach((id) => prevSet.delete(id));
      } else {
        ids.forEach((id) => prevSet.add(id));
      }
      return Array.from(prevSet);
    });
  };

  const onUploadScriptFile = async (file: File) => {
    const text = await file.text();
    setScriptContent(text);
    const detected = inferInterpreterFromFileName(file.name);
    if (detected) {
      setInterpreter(detected);
    }
  };

  const handleRun = async () => {
    if (!selectedDeviceIds.length || !scriptContent.trim()) return;
    try {
      await runOnDevices({
        machineIds: selectedDeviceIds,
        machineNamesById,
        scriptContent,
        interpreter,
      });
    } catch {
      /* lastInvokeError from hook surfaces the message */
    }
  };

  return (
    <>
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <div>
          <h1 className="text-xl font-semibold text-white">Script Deployment</h1>
          <p className="text-slate-500 text-xs">
            Deploy scripts to multiple devices and monitor output in real time
          </p>
        </div>
        <div className="flex flex-col items-end gap-1">
          {lastInvokeError && (
            <span className="text-xs text-amber-300 max-w-md text-right">{lastInvokeError}</span>
          )}
          <div className="flex items-center gap-2">
          <span
            className={`text-xs px-2 py-1 rounded-full ${
              connectionState === "connected"
                ? "bg-emerald-600/20 text-emerald-300"
                : connectionState === "connecting"
                  ? "bg-blue-600/20 text-blue-300"
                  : "bg-rose-600/20 text-rose-300"
            }`}
          >
            SignalR: {connectionState}
          </span>
          <button
            type="button"
            className="bg-slate-700 hover:bg-slate-600 text-white px-3 py-2 rounded-lg text-xs"
            onClick={clearFinished}
          >
            Clear Finished
          </button>
          </div>
        </div>
      </header>

      <div className="flex-1 overflow-auto p-6 md:p-8 space-y-6">
        <section className="bg-slate-900 border border-slate-800 rounded-xl p-4 space-y-4">
          <div className="flex flex-wrap gap-3 items-end">
            <div>
              <label className="block text-xs text-slate-400 mb-1.5">Interpreter</label>
              <select
                value={interpreter}
                onChange={(e) => setInterpreter(e.target.value as ScriptInterpreter)}
                className="bg-slate-800 border border-slate-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:border-primary-500"
              >
                <option value="powershell">PowerShell</option>
                <option value="bash">Bash</option>
              </select>
            </div>

            <div>
              <label className="block text-xs text-slate-400 mb-1.5">Upload Script File</label>
              <input
                ref={fileInputRef}
                type="file"
                accept=".ps1,.sh,.txt"
                className="hidden"
                onChange={async (e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  await onUploadScriptFile(file);
                  e.currentTarget.value = "";
                }}
              />
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                className="bg-slate-800 border border-slate-700 hover:bg-slate-700 text-white text-sm rounded-lg px-3 py-2"
              >
                Upload Script File
              </button>
            </div>

            <button
              type="button"
              className="ml-auto bg-primary-600 hover:bg-primary-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg text-sm font-medium"
              disabled={!selectedDeviceIds.length || !scriptContent.trim()}
              onClick={handleRun}
            >
              Run on Selected Devices ({selectedDeviceIds.length})
            </button>
            <button
              type="button"
              className="bg-rose-600 hover:bg-rose-500 text-white px-4 py-2 rounded-lg text-sm font-medium"
              onClick={stopAll}
            >
              Stop All
            </button>
          </div>

          <div className="h-[280px] border border-slate-700 rounded-lg overflow-hidden">
            <Editor
              height="100%"
              language={monacoLanguage(interpreter)}
              value={scriptContent}
              onChange={(value) => setScriptContent(value ?? "")}
              options={{
                minimap: { enabled: false },
                fontSize: 13,
                automaticLayout: true,
                wordWrap: "on",
              }}
              theme="vs-dark"
            />
          </div>
        </section>

        <section className="bg-slate-900 border border-slate-800 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-white mb-3">Target Selection</h2>
          {devicesQuery.isLoading ? (
            <p className="text-slate-400 text-sm">Loading devices…</p>
          ) : devicesQuery.isError ? (
            <p className="text-rose-300 text-sm">Failed to load devices.</p>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              <div className="border border-slate-800 rounded-lg p-3">
                <h3 className="text-xs uppercase tracking-wide text-slate-400 mb-2">Groups</h3>
                <div className="space-y-2 max-h-52 overflow-auto pr-1">
                  {groups.map((group) => {
                    const groupSelectedCount = group.deviceIds.filter((id) =>
                      selectedIdSet.has(id),
                    ).length;
                    const allSelected =
                      group.deviceIds.length > 0 &&
                      groupSelectedCount === group.deviceIds.length;
                    return (
                      <label
                        key={group.id}
                        className="flex items-center justify-between text-sm text-slate-200 bg-slate-800/70 rounded px-2 py-1.5"
                      >
                        <span>{group.name}</span>
                        <span className="flex items-center gap-2">
                          <span className="text-xs text-slate-400">
                            {groupSelectedCount}/{group.deviceIds.length}
                          </span>
                          <input
                            type="checkbox"
                            checked={allSelected}
                            onChange={() => toggleGroup(group.deviceIds)}
                          />
                        </span>
                      </label>
                    );
                  })}
                </div>
              </div>

              <div className="border border-slate-800 rounded-lg p-3">
                <h3 className="text-xs uppercase tracking-wide text-slate-400 mb-2">Devices</h3>
                <div className="space-y-2 max-h-52 overflow-auto pr-1">
                  {devices.map((device) => (
                    <label
                      key={device.id}
                      className="flex items-center justify-between text-sm text-slate-200 bg-slate-800/70 rounded px-2 py-1.5"
                    >
                      <span className="truncate pr-2">
                        {device.hostname}
                        <span className="text-xs text-slate-400 ml-2">
                          ({device.groupName || "Ungrouped"})
                        </span>
                      </span>
                      <input
                        type="checkbox"
                        checked={selectedIdSet.has(device.id)}
                        onChange={() => toggleDevice(device.id)}
                      />
                    </label>
                  ))}
                </div>
              </div>
            </div>
          )}
        </section>

        <section className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
          <div className="px-4 py-3 border-b border-slate-800 flex items-center justify-between">
            <h2 className="text-sm font-semibold text-white">Execution Monitor</h2>
            {connectionError && (
              <span className="text-xs text-rose-300">SignalR error: {connectionError}</span>
            )}
          </div>

          <div className="overflow-auto">
            <table className="w-full text-sm">
              <thead className="bg-slate-850 text-slate-400 text-xs uppercase tracking-wide">
                <tr>
                  <th className="text-left px-4 py-2">Machine Name</th>
                  <th className="text-left px-4 py-2">Task</th>
                  <th className="text-left px-4 py-2">Current Status</th>
                  <th className="text-left px-4 py-2">Progress</th>
                  <th className="text-right px-4 py-2">Action</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 ? (
                  <tr>
                    <td className="px-4 py-6 text-slate-400" colSpan={5}>
                      No script executions yet.
                    </td>
                  </tr>
                ) : (
                  rows.map((row) => {
                    const rowKey = `${row.taskId}::${row.machineId}`;
                    return (
                      <tr key={rowKey} className="border-t border-slate-800">
                        <td className="px-4 py-3 text-white">{row.machineName}</td>
                        <td className="px-4 py-3 text-slate-300 font-mono text-xs">{row.taskId}</td>
                        <td className="px-4 py-3">
                          <span
                            className={`inline-flex px-2 py-1 rounded-full text-xs ${STATUS_BADGE_CLASS[row.status]}`}
                          >
                            {STATUS_LABEL[row.status]}
                          </span>
                        </td>
                        <td className="px-4 py-3 min-w-[180px]">
                          <div className="w-full h-2 bg-slate-800 rounded">
                            <div
                              className="h-2 bg-primary-500 rounded"
                              style={{ width: `${Math.max(0, Math.min(100, row.progress))}%` }}
                            />
                          </div>
                          <span className="text-xs text-slate-400 mt-1 inline-block">
                            {Math.round(row.progress)}%
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right space-x-2">
                          <button
                            type="button"
                            className="bg-slate-800 hover:bg-slate-700 text-white text-xs px-3 py-1.5 rounded"
                            onClick={() => setActiveLogRowKey(rowKey)}
                          >
                            View Log
                          </button>
                          <button
                            type="button"
                            className="bg-rose-600/80 hover:bg-rose-600 text-white text-xs px-3 py-1.5 rounded"
                            onClick={() => cancelMachine(row.taskId, row.machineId)}
                            disabled={row.status !== "pending" && row.status !== "running"}
                          >
                            Cancel
                          </button>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {activeLogRow && (
        <div
          className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4"
          onClick={() => setActiveLogRowKey(null)}
        >
          <div
            className="w-full max-w-4xl bg-slate-900 border border-slate-700 rounded-xl shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between px-4 py-3 border-b border-slate-800">
              <div>
                <h3 className="text-white font-semibold text-sm">
                  Log Stream - {activeLogRow.machineName}
                </h3>
                <p className="text-slate-400 text-xs font-mono">{activeLogRow.taskId}</p>
              </div>
              <button
                type="button"
                onClick={() => setActiveLogRowKey(null)}
                className="text-slate-400 hover:text-white text-sm"
              >
                Close
              </button>
            </div>
            <div className="p-4">
              <pre className="bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs text-slate-200 overflow-auto max-h-[60vh] whitespace-pre-wrap">
                {activeLogRow.logLines.join("\n") || "No log output yet."}
              </pre>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
