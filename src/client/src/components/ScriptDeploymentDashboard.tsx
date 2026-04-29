import { useMemo, useRef, useState } from "react";
import Editor from "@monaco-editor/react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useVirtualizer } from "@tanstack/react-virtual";
import {
  Activity,
  AlertTriangle,
  Ban,
  CheckCircle2,
  Clock3,
  LoaderCircle,
  Search,
  Save,
  Square,
  TerminalSquare,
  XCircle,
  Calendar,
  History,
  Trash2,
} from "lucide-react";
import { devicesQueryKey, fetchDevices } from "../api/devices";
import {
  createScheduledScript,
  deleteScheduledScript,
  fetchScheduledScripts,
  scheduledScriptsQueryKey,
  updateScheduledScript,
} from "../api/scheduledScripts";
import {
  createSavedScript,
  deleteSavedScript,
  fetchSavedScripts,
  savedScriptsQueryKey,
  updateSavedScript,
} from "../api/savedScripts";
import { extractApiErrorMessage } from "../api/scriptRunner";
import type { DeviceDto } from "../types/device";
import type { SavedScript } from "../types/savedScripts";
import {
  type ScriptInterpreter,
  type ScriptExecutionStatus,
  useMultiDeviceScriptRunner,
} from "../hooks/useMultiDeviceScriptRunner";
import { CreateScheduleModal } from "./CreateScheduleModal";
import {
  ScheduledScriptTargetType,
  type ScheduledScriptDto,
} from "../types/scheduledScripts";

const DEFAULT_SCRIPT = ["# Write your deployment script here", ""].join("\n");

const STATUS_STYLE: Record<
  ScriptExecutionStatus,
  { badge: string; icon: typeof Activity }
> = {
  pending: { badge: "bg-slate-700 text-slate-200", icon: Clock3 },
  running: { badge: "bg-blue-600/20 text-blue-300", icon: LoaderCircle },
  success: { badge: "bg-emerald-600/20 text-emerald-300", icon: CheckCircle2 },
  error: { badge: "bg-rose-600/20 text-rose-300", icon: XCircle },
  timeout: { badge: "bg-amber-600/20 text-amber-300", icon: AlertTriangle },
  cancelled: { badge: "bg-orange-600/20 text-orange-300", icon: Ban },
};

const STATUS_LABEL: Record<ScriptExecutionStatus, string> = {
  pending: "Pending",
  running: "Running",
  success: "Success",
  error: "Error",
  timeout: "Timeout",
  cancelled: "Cancelled",
};

const monacoLanguage = (interpreter: ScriptInterpreter) =>
  interpreter === "powershell"
    ? "powershell"
    : interpreter === "cmd"
      ? "bat"
      : "shell";

const inferInterpreterFromFileName = (
  name: string,
): ScriptInterpreter | null => {
  const lower = name.toLowerCase();
  if (lower.endsWith(".ps1")) return "powershell";
  if (lower.endsWith(".sh")) return "bash";
  if (lower.endsWith(".cmd") || lower.endsWith(".bat")) return "cmd";
  return null;
};

const buildDeviceGroups = (devices: DeviceDto[]) => {
  const map = new Map<
    string,
    { id: string; name: string; deviceIds: string[] }
  >();
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

const STREAM_COLORS: Record<string, string> = {
  stdout: "text-slate-200",
  stderr: "text-rose-300",
  system: "text-amber-300",
};

const parseLogLine = (line: string) => {
  const match = line.match(/^\[(?<stream>[^\]]+)\]\s?(?<message>.*)$/);
  const stream = match?.groups?.stream?.toLowerCase() || "system";
  const message = match?.groups?.message || line;
  return { stream, message };
};

export function ScriptDeploymentDashboard() {
  const queryClient = useQueryClient();
  const [scriptContent, setScriptContent] = useState(DEFAULT_SCRIPT);
  const [interpreter, setInterpreter] =
    useState<ScriptInterpreter>("powershell");
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<string[]>([]);
  const [activeLogRowKey, setActiveLogRowKey] = useState<string | null>(null);
  const [deviceSearch, setDeviceSearch] = useState("");
  const [monitorSearch, setMonitorSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<
    "all" | ScriptExecutionStatus
  >("all");
  const [showActiveOnly, setShowActiveOnly] = useState(false);
  const [logSearch, setLogSearch] = useState("");
  const [logStreamFilter, setLogStreamFilter] = useState<
    "all" | "stdout" | "stderr" | "system"
  >("all");
  const [savedScriptsSearch, setSavedScriptsSearch] = useState("");
  const [activeSavedScriptId, setActiveSavedScriptId] = useState<string | null>(
    null,
  );
  const [loadedScriptMeta, setLoadedScriptMeta] = useState<{
    id: string;
    title: string;
    description: string;
  } | null>(null);
  const [isSaveModalOpen, setIsSaveModalOpen] = useState(false);
  const [saveModalTitle, setSaveModalTitle] = useState("");
  const [saveModalDescription, setSaveModalDescription] = useState("");
  const [saveModalTargetId, setSaveModalTargetId] = useState<string | null>(
    null,
  );
  const [savedScriptsError, setSavedScriptsError] = useState<string | null>(
    null,
  );
  const [isScheduleModalOpen, setIsScheduleModalOpen] = useState(false);
  const [editingSchedule, setEditingSchedule] =
    useState<ScheduledScriptDto | null>(null);
  const [activeTab, setActiveTab] = useState<"library" | "schedules">(
    "library",
  );
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const monitorContainerRef = useRef<HTMLDivElement | null>(null);
  const logContainerRef = useRef<HTMLDivElement | null>(null);

  const devicesQuery = useQuery({
    queryKey: devicesQueryKey,
    queryFn: fetchDevices,
  });
  const savedScriptsQuery = useQuery({
    queryKey: savedScriptsQueryKey,
    queryFn: fetchSavedScripts,
  });
  const scheduledScriptsQuery = useQuery({
    queryKey: scheduledScriptsQueryKey,
    queryFn: fetchScheduledScripts,
  });

  const createScheduleMutation = useMutation({
    mutationFn: createScheduledScript,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scheduledScriptsQueryKey });
      setIsScheduleModalOpen(false);
      setEditingSchedule(null);
    },
  });

  const updateScheduleMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) =>
      updateScheduledScript(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scheduledScriptsQueryKey });
      setIsScheduleModalOpen(false);
      setEditingSchedule(null);
    },
  });

  const deleteScheduleMutation = useMutation({
    mutationFn: deleteScheduledScript,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scheduledScriptsQueryKey });
    },
  });

  const toggleScheduleMutation = useMutation({
    mutationFn: ({ id, isEnabled }: { id: string; isEnabled: boolean }) => {
      const schedule = scheduledScriptsQuery.data?.find((s) => s.id === id);
      if (!schedule) throw new Error("Schedule not found");
      return updateScheduledScript(id, {
        ...schedule,
        isEnabled,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scheduledScriptsQueryKey });
    },
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
  const selectedIdSet = useMemo(
    () => new Set(selectedDeviceIds),
    [selectedDeviceIds],
  );
  const filteredDevices = useMemo(() => {
    if (!deviceSearch.trim()) return devices;
    const query = deviceSearch.toLowerCase();
    return devices.filter((d) =>
      `${d.hostname} ${d.groupName || "ungrouped"} ${d.id}`
        .toLowerCase()
        .includes(query),
    );
  }, [devices, deviceSearch]);
  const filteredDeviceIdSet = useMemo(
    () => new Set(filteredDevices.map((d) => d.id)),
    [filteredDevices],
  );
  const machineNamesById = useMemo(() => {
    const map: Record<string, string> = {};
    devices.forEach((d) => {
      map[d.id] = d.hostname;
    });
    return map;
  }, [devices]);

  const activeLogRow = useMemo(() => {
    if (!activeLogRowKey) return null;
    return (
      rows.find(
        (row) => `${row.taskId}::${row.machineId}` === activeLogRowKey,
      ) ?? null
    );
  }, [activeLogRowKey, rows]);
  const countsByStatus = useMemo(() => {
    const base: Record<ScriptExecutionStatus, number> = {
      pending: 0,
      running: 0,
      success: 0,
      error: 0,
      timeout: 0,
      cancelled: 0,
    };
    rows.forEach((row) => {
      base[row.status] += 1;
    });
    return base;
  }, [rows]);
  const activeCount = countsByStatus.running + countsByStatus.pending;
  const selectedSummary = `${selectedDeviceIds.length} selected / ${devices.length} total`;
  const savedScripts = savedScriptsQuery.data || [];
  const scheduledScripts = scheduledScriptsQuery.data || [];

  const filteredSavedScripts = useMemo(() => {
    if (!savedScriptsSearch.trim()) return savedScripts;
    const query = savedScriptsSearch.trim().toLowerCase();
    return savedScripts.filter((script) =>
      script.title.toLowerCase().includes(query),
    );
  }, [savedScripts, savedScriptsSearch]);
  const activeSavedScript = useMemo(
    () =>
      savedScripts.find((script) => script.id === activeSavedScriptId) ?? null,
    [savedScripts, activeSavedScriptId],
  );
  const monitorRows = useMemo(() => {
    const query = monitorSearch.trim().toLowerCase();
    return rows.filter((row) => {
      if (
        showActiveOnly &&
        row.status !== "running" &&
        row.status !== "pending"
      )
        return false;
      if (statusFilter !== "all" && row.status !== statusFilter) return false;
      if (!query) return true;
      return `${row.machineName} ${row.taskId} ${row.interpreter || ""}`
        .toLowerCase()
        .includes(query);
    });
  }, [rows, monitorSearch, showActiveOnly, statusFilter]);

  const monitorVirtualizer = useVirtualizer({
    count: monitorRows.length,
    getScrollElement: () => monitorContainerRef.current,
    estimateSize: () => 56,
    overscan: 12,
  });

  const parsedActiveLogs = useMemo(() => {
    if (!activeLogRow) return [];
    const query = logSearch.trim().toLowerCase();
    return activeLogRow.logLines
      .map((line, index) => ({ ...parseLogLine(line), raw: line, index }))
      .filter((line) => {
        if (logStreamFilter !== "all" && line.stream !== logStreamFilter)
          return false;
        if (!query) return true;
        return line.raw.toLowerCase().includes(query);
      });
  }, [activeLogRow, logSearch, logStreamFilter]);

  const logVirtualizer = useVirtualizer({
    count: parsedActiveLogs.length,
    getScrollElement: () => logContainerRef.current,
    estimateSize: () => 22,
    overscan: 20,
  });

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
    setLoadedScriptMeta(null);
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

  const handleScheduleSave = (data: {
    name: string;
    cronExpression?: string;
    runAt?: string;
    targetType: ScheduledScriptTargetType;
    targetId: string;
  }) => {
    const payload = {
      ...data,
      scriptContent,
      interpreterType:
        interpreter === "powershell" ? 0 : interpreter === "bash" ? 1 : 2,
      arguments: [],
      timeoutSeconds: 300,
    };

    if (editingSchedule) {
      updateScheduleMutation.mutate({
        id: editingSchedule.id,
        data: { ...payload, isEnabled: editingSchedule.isEnabled },
      });
    } else {
      createScheduleMutation.mutate(payload);
    }
  };
  const createSavedScriptMutation = useMutation({
    mutationFn: createSavedScript,
    onSuccess: (created) => {
      setSavedScriptsError(null);
      setActiveSavedScriptId(created.id);
      setLoadedScriptMeta({
        id: created.id,
        title: created.title,
        description: created.description || "",
      });
      setIsSaveModalOpen(false);
      queryClient.invalidateQueries({ queryKey: savedScriptsQueryKey });
    },
    onError: (error: unknown) => {
      setSavedScriptsError(extractApiErrorMessage(error));
    },
  });

  const updateSavedScriptMutation = useMutation({
    mutationFn: ({
      id,
      payload,
    }: {
      id: string;
      payload: Parameters<typeof updateSavedScript>[1];
    }) => updateSavedScript(id, payload),
    onSuccess: (updated) => {
      setSavedScriptsError(null);
      setActiveSavedScriptId(updated.id);
      setLoadedScriptMeta({
        id: updated.id,
        title: updated.title,
        description: updated.description || "",
      });
      setIsSaveModalOpen(false);
      queryClient.invalidateQueries({ queryKey: savedScriptsQueryKey });
    },
    onError: (error: unknown) => {
      setSavedScriptsError(extractApiErrorMessage(error));
    },
  });

  const deleteSavedScriptMutation = useMutation({
    mutationFn: deleteSavedScript,
    onSuccess: (_, id) => {
      setSavedScriptsError(null);
      if (activeSavedScriptId === id) {
        setActiveSavedScriptId(null);
      }
      if (loadedScriptMeta?.id === id) {
        setLoadedScriptMeta(null);
      }
      queryClient.invalidateQueries({ queryKey: savedScriptsQueryKey });
    },
    onError: (error: unknown) => {
      setSavedScriptsError(extractApiErrorMessage(error));
    },
  });

  const persistCurrentScript = async () => {
    const title = saveModalTitle.trim();
    if (!title || !scriptContent.trim()) {
      setSavedScriptsError("Title and script content are required.");
      return;
    }

    const payload = {
      title,
      description: saveModalDescription.trim() || null,
      content: scriptContent,
      interpreter,
    };

    if (saveModalTargetId) {
      await updateSavedScriptMutation.mutateAsync({
        id: saveModalTargetId,
        payload,
      });
      return;
    }

    await createSavedScriptMutation.mutateAsync(payload);
  };

  const loadSavedScript = (script: SavedScript) => {
    setScriptContent(script.content);
    setInterpreter(script.interpreter);
    setActiveSavedScriptId(script.id);
    setLoadedScriptMeta({
      id: script.id,
      title: script.title,
      description: script.description || "",
    });
  };

  const runSavedScript = async (script: SavedScript) => {
    if (!selectedDeviceIds.length) {
      setSavedScriptsError("Select at least one device before running.");
      return;
    }
    setSavedScriptsError(null);
    await runOnDevices({
      machineIds: selectedDeviceIds,
      machineNamesById,
      scriptContent: script.content,
      interpreter: script.interpreter,
    });
  };

  const openSaveModal = () => {
    setSavedScriptsError(null);
    if (loadedScriptMeta) {
      setSaveModalTitle(loadedScriptMeta.title);
      setSaveModalDescription(loadedScriptMeta.description);
      setSaveModalTargetId(loadedScriptMeta.id);
    } else {
      setSaveModalTitle("");
      setSaveModalDescription("");
      setSaveModalTargetId(null);
    }
    setIsSaveModalOpen(true);
  };

  const toggleSelectAllFiltered = () => {
    const ids = filteredDevices.map((d) => d.id);
    const allSelected =
      ids.length > 0 && ids.every((id) => selectedIdSet.has(id));
    setSelectedDeviceIds((prev) => {
      const next = new Set(prev);
      if (allSelected) {
        ids.forEach((id) => next.delete(id));
      } else {
        ids.forEach((id) => next.add(id));
      }
      return Array.from(next);
    });
  };

  const handleStopAll = async () => {
    if (activeCount === 0) return;
    const confirmed = window.confirm(
      `Stop ${activeCount} active execution(s)?`,
    );
    if (!confirmed) return;
    await stopAll();
  };

  return (
    <>
      <header className="border-b border-slate-800/80 px-6 md:px-8 py-3 bg-slate-900/80 shrink-0 space-y-3 backdrop-blur-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-lg font-semibold text-white">
              Script Deployment
            </h1>
            <p className="text-slate-500 text-xs">
              Execute scripts across devices and monitor outcomes in real time.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <span
              className={`text-[11px] px-2 py-1 rounded-full border ${
                connectionState === "connected"
                  ? "border-emerald-500/40 bg-emerald-500/10 text-emerald-300"
                  : connectionState === "connecting"
                    ? "border-blue-500/40 bg-blue-500/10 text-blue-300"
                    : "border-rose-500/40 bg-rose-500/10 text-rose-300"
              }`}
            >
              SignalR: {connectionState}
            </span>
            <button
              type="button"
              className="bg-slate-800/80 border border-slate-700 hover:bg-slate-700/80 disabled:opacity-50 text-white px-2.5 py-1.5 rounded-lg text-[11px]"
              onClick={clearFinished}
              disabled={rows.length === 0}
            >
              Clear Finished
            </button>
            <button
              type="button"
              className="bg-rose-600/90 hover:bg-rose-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-3 py-1.5 rounded-lg text-[11px] font-medium"
              onClick={handleStopAll}
              disabled={activeCount === 0}
              title="Stop all running and pending executions"
            >
              <span className="inline-flex items-center gap-1.5">
                <Square className="h-3 w-3" />
                Stop Active
              </span>
            </button>
          </div>
        </div>
        <div className="grid grid-cols-3 md:grid-cols-6 gap-1.5">
          {(
            [
              ["running", countsByStatus.running, "Running"],
              ["pending", countsByStatus.pending, "Pending"],
              ["error", countsByStatus.error, "Errors"],
              ["timeout", countsByStatus.timeout, "Timeouts"],
              ["cancelled", countsByStatus.cancelled, "Cancelled"],
              ["success", countsByStatus.success, "Success"],
            ] as const
          ).map(([status, count, label]) => {
            const Icon = STATUS_STYLE[status].icon;
            return (
              <button
                key={status}
                type="button"
                onClick={() => {
                  setStatusFilter((current) =>
                    current === status
                      ? "all"
                      : (status as ScriptExecutionStatus),
                  );
                  setShowActiveOnly(false);
                }}
                className={`rounded-md border px-2.5 py-1.5 text-left transition ${
                  statusFilter === status
                    ? "border-primary-500/60 bg-primary-500/10"
                    : "border-slate-800 bg-slate-900/70 hover:bg-slate-800/60"
                }`}
                title={`Filter executions by ${label.toLowerCase()}`}
              >
                <div className="flex items-center justify-between">
                  <span className="text-[11px] text-slate-400">{label}</span>
                  <Icon
                    className={`h-3.5 w-3.5 ${STATUS_STYLE[status].badge.split(" ").at(-1) || "text-slate-300"} ${
                      status === "running" && count > 0 ? "animate-spin" : ""
                    }`}
                  />
                </div>
                <div className="text-sm font-semibold text-white leading-tight">
                  {count}
                </div>
              </button>
            );
          })}
        </div>
        <div className="min-h-[16px] flex items-center justify-between">
          {lastInvokeError && (
            <span className="text-[11px] text-amber-300 inline-flex items-center gap-1.5">
              <AlertTriangle className="h-3 w-3" />
              {lastInvokeError}
            </span>
          )}
          {!lastInvokeError && <span />}
          {connectionError && (
            <span className="text-[11px] text-rose-300">{connectionError}</span>
          )}
        </div>
      </header>

      <div className="flex-1 overflow-auto p-6 md:p-8 space-y-6">
        <section className="bg-slate-900 border border-slate-800 rounded-xl p-4 space-y-3">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <div className="flex gap-4 border-b border-slate-800 mb-2">
                <button
                  onClick={() => setActiveTab("library")}
                  className={`pb-2 text-sm font-semibold transition-colors ${
                    activeTab === "library"
                      ? "text-primary-500 border-b-2 border-primary-500"
                      : "text-slate-400 hover:text-slate-200"
                  }`}
                >
                  Script Library
                </button>
                <button
                  onClick={() => setActiveTab("schedules")}
                  className={`pb-2 text-sm font-semibold transition-colors ${
                    activeTab === "schedules"
                      ? "text-primary-500 border-b-2 border-primary-500"
                      : "text-slate-400 hover:text-slate-200"
                  }`}
                >
                  Schedules
                </button>
              </div>
              <p className="text-xs text-slate-400">
                {activeTab === "library"
                  ? "Quickly pick, open, and run reusable scripts."
                  : "Manage automated script executions."}
              </p>
            </div>
            <div className="relative">
              <Search className="h-3.5 w-3.5 text-slate-500 absolute left-2 top-1/2 -translate-y-1/2" />
              <input
                value={activeTab === "library" ? savedScriptsSearch : ""}
                onChange={(e) =>
                  activeTab === "library"
                    ? setSavedScriptsSearch(e.target.value)
                    : null
                }
                placeholder={
                  activeTab === "library"
                    ? "Search library..."
                    : "Search schedules..."
                }
                className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg pl-7 pr-2 py-2 w-60"
              />
            </div>
          </div>

          <div className="border border-slate-800 rounded-lg max-h-64 overflow-auto bg-slate-950/40">
            {activeTab === "library" ? (
              savedScriptsQuery.isLoading ? (
                <p className="text-sm text-slate-400 p-4">
                  Loading saved scripts…
                </p>
              ) : savedScriptsQuery.isError ? (
                <p className="text-sm text-rose-300 p-4">
                  Failed to load saved scripts.
                </p>
              ) : filteredSavedScripts.length === 0 ? (
                <p className="text-sm text-slate-400 p-4">
                  No saved scripts found.
                </p>
              ) : (
                <ul className="divide-y divide-slate-800">
                  {filteredSavedScripts.map((script) => {
                    const isSelected = activeSavedScriptId === script.id;
                    return (
                      <li key={script.id}>
                        <button
                          type="button"
                          onClick={() => setActiveSavedScriptId(script.id)}
                          className={`w-full px-4 py-3 text-left transition ${
                            isSelected
                              ? "bg-primary-600/15 border-l-2 border-primary-500"
                              : "hover:bg-slate-900/80 border-l-2 border-transparent"
                          }`}
                        >
                          <div className="flex items-start justify-between gap-2">
                            <div>
                              <p
                                className={`text-sm font-medium ${isSelected ? "text-white" : "text-slate-200"}`}
                              >
                                {script.title}
                              </p>
                              {script.description && (
                                <p className="text-xs text-slate-400 mt-1 line-clamp-1">
                                  {script.description}
                                </p>
                              )}
                            </div>
                            <div className="text-right">
                              <span className="text-[11px] px-2 py-0.5 rounded bg-slate-800 text-slate-300 uppercase">
                                {script.interpreter}
                              </span>
                              <p className="text-[11px] text-slate-500 mt-1">
                                {new Date(
                                  script.updatedAt,
                                ).toLocaleDateString()}
                              </p>
                            </div>
                          </div>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )
            ) : scheduledScriptsQuery.isLoading ? (
              <p className="text-sm text-slate-400 p-4">Loading schedules…</p>
            ) : scheduledScriptsQuery.isError ? (
              <p className="text-sm text-rose-300 p-4">
                Failed to load schedules.
              </p>
            ) : scheduledScripts.length === 0 ? (
              <p className="text-sm text-slate-400 p-4">No schedules found.</p>
            ) : (
              <ul className="divide-y divide-slate-800">
                {scheduledScripts.map((s) => (
                  <li
                    key={s.id}
                    className="px-4 py-3 flex items-center justify-between gap-4"
                  >
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className="text-sm font-medium text-slate-200 truncate">
                          {s.name}
                        </p>
                        <span
                          className={`text-[10px] px-1.5 py-0.5 rounded-full ${s.isEnabled ? "bg-emerald-500/10 text-emerald-400" : "bg-slate-800 text-slate-400"}`}
                        >
                          {s.isEnabled ? "Active" : "Disabled"}
                        </span>
                      </div>
                      <div className="flex items-center gap-3 mt-1 text-[11px] text-slate-400">
                        <span className="flex items-center gap-1">
                          <Clock3 className="h-3 w-3" />
                          {s.cronExpression
                            ? `Cron: ${s.cronExpression}`
                            : `Once: ${new Date(s.runAt!).toLocaleString()}`}
                        </span>
                        {s.nextRunAt && (
                          <span className="flex items-center gap-1">
                            <History className="h-3 w-3" />
                            Next: {new Date(s.nextRunAt).toLocaleString()}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => {
                          setEditingSchedule(s);
                          setIsScheduleModalOpen(true);
                        }}
                        className="text-xs px-2 py-1 rounded border border-slate-700 text-slate-300 hover:bg-slate-800"
                      >
                        Edit
                      </button>
                      <button
                        onClick={() =>
                          toggleScheduleMutation.mutate({
                            id: s.id,
                            isEnabled: !s.isEnabled,
                          })
                        }
                        className={`text-xs px-2 py-1 rounded border transition ${
                          s.isEnabled
                            ? "border-amber-500/30 text-amber-400 hover:bg-amber-500/10"
                            : "border-emerald-500/30 text-emerald-400 hover:bg-emerald-500/10"
                        }`}
                      >
                        {s.isEnabled ? "Disable" : "Enable"}
                      </button>
                      <button
                        onClick={() => {
                          if (window.confirm("Delete this schedule?"))
                            deleteScheduleMutation.mutate(s.id);
                        }}
                        className="p-1.5 text-slate-500 hover:text-rose-400 hover:bg-rose-400/10 rounded-lg transition"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="border border-slate-800 rounded-lg p-3 bg-slate-900/70">
            {activeSavedScript ? (
              <div className="space-y-2">
                <p className="text-xs text-slate-400">
                  Selected:{" "}
                  <span className="text-slate-200">
                    {activeSavedScript.title}
                  </span>
                </p>
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    className="bg-slate-800 border border-slate-700 hover:bg-slate-700 text-white text-xs px-3 py-1.5 rounded"
                    onClick={() => loadSavedScript(activeSavedScript)}
                  >
                    Open in editor
                  </button>
                  <button
                    type="button"
                    className="bg-primary-600/90 hover:bg-primary-500 text-white text-xs px-3 py-1.5 rounded disabled:opacity-50"
                    onClick={() => runSavedScript(activeSavedScript)}
                    disabled={!selectedDeviceIds.length}
                  >
                    Run script
                  </button>
                  <button
                    type="button"
                    className="bg-rose-700/80 hover:bg-rose-600 text-white text-xs px-3 py-1.5 rounded disabled:opacity-50"
                    onClick={() => {
                      if (
                        window.confirm(
                          `Delete saved script "${activeSavedScript.title}"?`,
                        )
                      ) {
                        deleteSavedScriptMutation.mutate(activeSavedScript.id);
                      }
                    }}
                    disabled={deleteSavedScriptMutation.isPending}
                  >
                    Delete
                  </button>
                </div>
              </div>
            ) : (
              <p className="text-xs text-slate-400">
                Select a script to view available actions.
              </p>
            )}
          </div>
          {savedScriptsError && (
            <p className="text-xs text-rose-300">{savedScriptsError}</p>
          )}
        </section>

        <section className="bg-slate-900 border border-slate-800 rounded-xl p-4 space-y-4">
          <div className="flex flex-wrap gap-3 items-end">
            <div>
              <label className="block text-xs text-slate-400 mb-1.5">
                Interpreter
              </label>
              <select
                value={interpreter}
                onChange={(e) =>
                  setInterpreter(e.target.value as ScriptInterpreter)
                }
                className="bg-slate-800 border border-slate-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:border-primary-500"
              >
                <option value="powershell">PowerShell</option>
                <option value="bash">Bash</option>
                <option value="cmd">CMD</option>
              </select>
            </div>

            <div>
              <label className="block text-xs text-slate-400 mb-1.5">
                Upload Script File
              </label>
              <input
                ref={fileInputRef}
                type="file"
                accept=".ps1,.sh,.cmd,.bat,.txt"
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
              className="bg-slate-800 border border-slate-700 hover:bg-slate-700 disabled:opacity-50 disabled:cursor-not-allowed text-white px-3 py-2 rounded-lg text-sm"
              disabled={
                !scriptContent.trim() ||
                createSavedScriptMutation.isPending ||
                updateSavedScriptMutation.isPending
              }
              onClick={openSaveModal}
              title="Save the script currently in the editor"
            >
              <span className="inline-flex items-center gap-1.5">
                <Save className="h-4 w-4" />
                Save
              </span>
            </button>

            <button
              type="button"
              className="bg-primary-600 hover:bg-primary-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg text-sm font-medium"
              disabled={!selectedDeviceIds.length || !scriptContent.trim()}
              onClick={() => setIsScheduleModalOpen(true)}
              title={
                !selectedDeviceIds.length
                  ? "Select at least one device."
                  : "Schedule script"
              }
            >
              <span className="inline-flex items-center gap-1.5">
                <Calendar className="h-4 w-4" />
                Schedule
              </span>
            </button>

            <button
              type="button"
              className="bg-primary-600 hover:bg-primary-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg text-sm font-medium"
              disabled={!selectedDeviceIds.length || !scriptContent.trim()}
              onClick={handleRun}
              title={
                !selectedDeviceIds.length
                  ? "Select at least one device."
                  : "Dispatch script"
              }
            >
              <span className="inline-flex items-center gap-1.5">
                <TerminalSquare className="h-4 w-4" />
                Run on {selectedDeviceIds.length} device(s)
              </span>
            </button>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
            <span className="text-slate-400">{selectedSummary}</span>
            <span className="text-slate-500">
              Active executions:{" "}
              <span className="text-slate-300">{activeCount}</span>
            </span>
          </div>
          {loadedScriptMeta && (
            <div className="border border-slate-800 rounded-lg bg-slate-950/40 px-3 py-2">
              <p className="text-xs uppercase tracking-wide text-slate-500">
                Loaded Saved Script
              </p>
              <p className="text-sm text-white font-medium">
                {loadedScriptMeta.title}
              </p>
              {loadedScriptMeta.description && (
                <p className="text-xs text-slate-400 mt-1">
                  {loadedScriptMeta.description}
                </p>
              )}
            </div>
          )}

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

        <CreateScheduleModal
          isOpen={isScheduleModalOpen}
          onClose={() => {
            setIsScheduleModalOpen(false);
            setEditingSchedule(null);
          }}
          onSave={handleScheduleSave}
          scriptTitle={loadedScriptMeta?.title || "Custom Script"}
          selectedDeviceIds={selectedDeviceIds}
          groups={groups}
          editData={editingSchedule}
        />

        <section className="bg-slate-900 border border-slate-800 rounded-xl p-4">
          <div className="flex flex-wrap items-end justify-between gap-3 mb-3">
            <div>
              <h2 className="text-sm font-semibold text-white">
                Target Selection
              </h2>
              <p className="text-xs text-slate-400">
                Filter and bulk-select devices quickly.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <div className="relative">
                <Search className="h-3.5 w-3.5 text-slate-500 absolute left-2 top-1/2 -translate-y-1/2" />
                <input
                  value={deviceSearch}
                  onChange={(e) => setDeviceSearch(e.target.value)}
                  placeholder="Search devices..."
                  className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg pl-7 pr-2 py-2 w-52"
                />
              </div>
              <button
                type="button"
                onClick={toggleSelectAllFiltered}
                className="bg-slate-800 border border-slate-700 hover:bg-slate-700 text-white text-xs rounded-lg px-3 py-2"
              >
                Toggle All Filtered
              </button>
            </div>
          </div>
          {devicesQuery.isLoading ? (
            <p className="text-slate-400 text-sm">Loading devices…</p>
          ) : devicesQuery.isError ? (
            <p className="text-rose-300 text-sm">Failed to load devices.</p>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              <div className="border border-slate-800 rounded-lg p-3">
                <h3 className="text-xs uppercase tracking-wide text-slate-400 mb-2">
                  Groups
                </h3>
                <div className="space-y-2 max-h-52 overflow-auto pr-1">
                  {groups.map((group) => {
                    const groupIdsInFilter = group.deviceIds.filter((id) =>
                      filteredDeviceIdSet.has(id),
                    );
                    if (groupIdsInFilter.length === 0) return null;
                    const groupSelectedCount = groupIdsInFilter.filter((id) =>
                      selectedIdSet.has(id),
                    ).length;
                    const allSelected =
                      groupIdsInFilter.length > 0 &&
                      groupSelectedCount === groupIdsInFilter.length;
                    return (
                      <label
                        key={group.id}
                        className="flex items-center justify-between text-sm text-slate-200 bg-slate-800/70 rounded px-2 py-1.5"
                      >
                        <span>{group.name}</span>
                        <span className="flex items-center gap-2">
                          <span className="text-xs text-slate-400">
                            {groupSelectedCount}/{groupIdsInFilter.length}
                          </span>
                          <input
                            type="checkbox"
                            checked={allSelected}
                            onChange={() => toggleGroup(groupIdsInFilter)}
                          />
                        </span>
                      </label>
                    );
                  })}
                </div>
              </div>

              <div className="border border-slate-800 rounded-lg p-3">
                <h3 className="text-xs uppercase tracking-wide text-slate-400 mb-2">
                  Devices
                </h3>
                <div className="space-y-2 max-h-52 overflow-auto pr-1">
                  {filteredDevices.map((device) => (
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
          <div className="px-4 py-3 border-b border-slate-800 space-y-3">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-sm font-semibold text-white">
                Execution Monitor
              </h2>
              <span className="text-xs text-slate-400">
                {monitorRows.length} shown
              </span>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <div className="relative">
                <Search className="h-3.5 w-3.5 text-slate-500 absolute left-2 top-1/2 -translate-y-1/2" />
                <input
                  value={monitorSearch}
                  onChange={(e) => setMonitorSearch(e.target.value)}
                  placeholder="Search machine / task / interpreter..."
                  className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg pl-7 pr-2 py-2 w-72"
                />
              </div>
              <select
                value={statusFilter}
                onChange={(e) =>
                  setStatusFilter(
                    e.target.value as "all" | ScriptExecutionStatus,
                  )
                }
                className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg px-2 py-2"
              >
                <option value="all">All statuses</option>
                <option value="running">Running</option>
                <option value="pending">Pending</option>
                <option value="error">Error</option>
                <option value="timeout">Timeout</option>
                <option value="cancelled">Cancelled</option>
                <option value="success">Success</option>
              </select>
              <label className="text-xs text-slate-300 inline-flex items-center gap-2 px-2 py-1.5 rounded border border-slate-700 bg-slate-800/80">
                <input
                  type="checkbox"
                  checked={showActiveOnly}
                  onChange={(e) => setShowActiveOnly(e.target.checked)}
                />
                Active only
              </label>
            </div>
          </div>

          <div className="px-4 py-2 border-b border-slate-800 text-[11px] uppercase tracking-wide text-slate-400 grid grid-cols-[2fr_2fr_1fr_1.5fr_1.5fr_180px] gap-3">
            <span>Machine</span>
            <span>Task ID</span>
            <span>Interpreter</span>
            <span>Status</span>
            <span>Progress</span>
            <span className="text-right">Actions</span>
          </div>
          <div ref={monitorContainerRef} className="h-[420px] overflow-auto">
            {monitorRows.length === 0 ? (
              <div className="px-4 py-8 text-slate-400 text-sm">
                No executions matching current filters.
              </div>
            ) : (
              <div
                className="relative"
                style={{
                  height: `${monitorVirtualizer.getTotalSize()}px`,
                }}
              >
                {monitorVirtualizer.getVirtualItems().map((virtualRow) => {
                  const row = monitorRows[virtualRow.index];
                  const rowKey = `${row.taskId}::${row.machineId}`;
                  const StatusIcon = STATUS_STYLE[row.status].icon;
                  return (
                    <div
                      key={rowKey}
                      className="absolute left-0 top-0 w-full border-b border-slate-800 px-4 py-2 grid grid-cols-[2fr_2fr_1fr_1.5fr_1.5fr_180px] gap-3 text-sm items-center"
                      style={{ transform: `translateY(${virtualRow.start}px)` }}
                    >
                      <span className="text-white truncate">
                        {row.machineName}
                      </span>
                      <span className="text-slate-300 font-mono text-xs truncate">
                        {row.taskId}
                      </span>
                      <span className="text-slate-300 text-xs uppercase">
                        {row.interpreter || "-"}
                      </span>
                      <span
                        className={`inline-flex items-center gap-1.5 px-2 py-1 rounded-full text-xs w-fit ${STATUS_STYLE[row.status].badge}`}
                      >
                        <StatusIcon
                          className={`h-3.5 w-3.5 ${row.status === "running" ? "animate-spin" : ""}`}
                        />
                        {STATUS_LABEL[row.status]}
                      </span>
                      <span>
                        <div className="w-full h-2 bg-slate-800 rounded">
                          <div
                            className="h-2 bg-primary-500 rounded"
                            style={{
                              width: `${Math.max(0, Math.min(100, row.progress))}%`,
                            }}
                          />
                        </div>
                        <span className="text-[11px] text-slate-400 mt-1 inline-block">
                          {Math.round(row.progress)}%
                        </span>
                      </span>
                      <span className="flex items-center justify-end gap-2">
                        <button
                          type="button"
                          className="bg-slate-800 hover:bg-slate-700 text-white text-xs px-2.5 py-1.5 rounded"
                          onClick={() => setActiveLogRowKey(rowKey)}
                        >
                          Log
                        </button>
                        <button
                          type="button"
                          className="bg-rose-600/80 hover:bg-rose-600 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs px-2.5 py-1.5 rounded"
                          onClick={() =>
                            cancelMachine(row.taskId, row.machineId)
                          }
                          disabled={
                            row.status !== "pending" && row.status !== "running"
                          }
                        >
                          Cancel
                        </button>
                      </span>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </section>
      </div>

      {isSaveModalOpen && (
        <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-slate-900 border border-slate-700 rounded-xl shadow-xl">
            <div className="px-4 py-3 border-b border-slate-800 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-white">Save Script</h3>
              <button
                type="button"
                onClick={() => setIsSaveModalOpen(false)}
                className="text-xs text-slate-400 hover:text-white"
              >
                Close
              </button>
            </div>
            <div className="p-4 space-y-3">
              <div>
                <label className="block text-xs text-slate-400 mb-1.5">
                  Script name *
                </label>
                <input
                  value={saveModalTitle}
                  onChange={(e) => setSaveModalTitle(e.target.value)}
                  placeholder="Enter script name"
                  className="w-full bg-slate-800 border border-slate-700 text-white text-sm rounded-lg px-3 py-2"
                />
              </div>
              <div>
                <label className="block text-xs text-slate-400 mb-1.5">
                  Description
                </label>
                <textarea
                  value={saveModalDescription}
                  onChange={(e) => setSaveModalDescription(e.target.value)}
                  placeholder="Optional description"
                  rows={3}
                  className="w-full bg-slate-800 border border-slate-700 text-white text-sm rounded-lg px-3 py-2 resize-none"
                />
              </div>
              {savedScriptsError && (
                <p className="text-xs text-rose-300">{savedScriptsError}</p>
              )}
              <div className="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  onClick={() => setIsSaveModalOpen(false)}
                  className="bg-slate-800 border border-slate-700 hover:bg-slate-700 text-white text-sm px-3 py-2 rounded-lg"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={persistCurrentScript}
                  disabled={
                    createSavedScriptMutation.isPending ||
                    updateSavedScriptMutation.isPending
                  }
                  className="bg-primary-600 hover:bg-primary-500 disabled:opacity-50 text-white text-sm px-3 py-2 rounded-lg"
                >
                  Save
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

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
                <p className="text-slate-400 text-xs font-mono">
                  {activeLogRow.taskId}
                </p>
                <p className="text-slate-500 text-xs mt-1">
                  {parsedActiveLogs.length} matching lines shown (from{" "}
                  {activeLogRow.logLines.length} retained).
                </p>
              </div>
              <button
                type="button"
                onClick={() => setActiveLogRowKey(null)}
                className="text-slate-400 hover:text-white text-sm"
              >
                Close
              </button>
            </div>
            <div className="p-4 space-y-3">
              <div className="flex flex-wrap items-center gap-2">
                <div className="relative">
                  <Search className="h-3.5 w-3.5 text-slate-500 absolute left-2 top-1/2 -translate-y-1/2" />
                  <input
                    value={logSearch}
                    onChange={(e) => setLogSearch(e.target.value)}
                    placeholder="Search logs..."
                    className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg pl-7 pr-2 py-2 w-64"
                  />
                </div>
                <select
                  value={logStreamFilter}
                  onChange={(e) =>
                    setLogStreamFilter(
                      e.target.value as "all" | "stdout" | "stderr" | "system",
                    )
                  }
                  className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg px-2 py-2"
                >
                  <option value="all">All streams</option>
                  <option value="stdout">stdout</option>
                  <option value="stderr">stderr</option>
                  <option value="system">system</option>
                </select>
              </div>
              <div
                ref={logContainerRef}
                className="bg-slate-950 border border-slate-800 rounded-lg overflow-auto h-[60vh]"
              >
                {parsedActiveLogs.length === 0 ? (
                  <p className="text-slate-400 text-xs p-3">
                    No log lines matching current filters.
                  </p>
                ) : (
                  <div
                    className="relative text-xs"
                    style={{
                      height: `${logVirtualizer.getTotalSize()}px`,
                    }}
                  >
                    {logVirtualizer.getVirtualItems().map((virtualLog) => {
                      const line = parsedActiveLogs[virtualLog.index];
                      const streamColor =
                        STREAM_COLORS[line.stream] || "text-slate-200";
                      return (
                        <div
                          key={`${line.index}-${virtualLog.index}`}
                          className="absolute left-0 top-0 w-full px-3 py-1 font-mono border-b border-slate-900/70"
                          style={{
                            transform: `translateY(${virtualLog.start}px)`,
                          }}
                        >
                          <span className="text-slate-500 mr-2">
                            {String(line.index + 1).padStart(4, " ")}
                          </span>
                          <span className={`mr-2 uppercase ${streamColor}`}>
                            [{line.stream}]
                          </span>
                          <span className={streamColor}>{line.message}</span>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
