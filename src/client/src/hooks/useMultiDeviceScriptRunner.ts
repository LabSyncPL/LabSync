import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { getToken } from "../auth/authStore";
import {
  cancelTask,
  executeScript,
  extractApiErrorMessage,
  SCRIPT_RUNNER_HUB_PATH,
  ScriptRunnerHubEvents,
} from "../api/scriptRunner";
import {
  ScriptInterpreterType,
  type ScriptInterpreterTypeNumber,
  type ScriptOutputTelemetryPayload,
  type ScriptTaskCompletedPayload,
} from "../types/scriptRunner";

const DEFAULT_BASE_URL = "http://localhost:5038";
const BASE_URL =
  (typeof import.meta !== "undefined" &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

/** While script is running, heuristic progress never exceeds this until TaskCompleted. */
const MAX_INFERRED_PROGRESS = 95;
const MAX_LOG_LINES_PER_ROW = 500;

export type ScriptInterpreter = "powershell" | "bash" | "cmd";
export type ScriptExecutionStatus =
  | "pending"
  | "running"
  | "success"
  | "error"
  | "timeout"
  | "cancelled";

export interface DeviceExecutionRow {
  taskId: string;
  machineId: string;
  machineName: string;
  status: ScriptExecutionStatus;
  progress: number;
  logLines: string[];
  lastUpdatedAt: number;
  interpreter?: string;
}

interface RunOnDevicesInput {
  machineIds: string[];
  machineNamesById: Record<string, string>;
  scriptContent: string;
  interpreter: ScriptInterpreter;
  timeoutSeconds?: number;
}

const makeRowKey = (taskId: string, machineId: string) => `${taskId}::${machineId}`;

const normalizeStatus = (raw?: string): ScriptExecutionStatus | null => {
  if (!raw) return null;
  const value = raw.toLowerCase();
  if (value.includes("success") || value.includes("completed")) return "success";
  if (value.includes("error") || value.includes("fail")) return "error";
  if (value.includes("cancel")) return "cancelled";
  if (value.includes("timeout")) return "timeout";
  if (value.includes("running") || value.includes("started")) return "running";
  if (value.includes("pending") || value.includes("queued")) return "pending";
  return null;
};

const inferStatusFromLine = (line: string, stream?: string): ScriptExecutionStatus | null => {
  const lower = line.toLowerCase();
  if (stream?.toLowerCase() === "stderr") return "error";
  if (lower.includes("error") || lower.includes("exception")) return "error";
  if (lower.includes("cancel")) return "cancelled";
  if (lower.includes("timed out") || lower.includes("timeout")) return "timeout";
  // Do not infer "success" from text — TaskCompleted is authoritative for 100% / final status.
  if (lower.includes("started") || lower.includes("running")) return "running";
  return null;
};

const inferProgress = (
  status: ScriptExecutionStatus,
  existingProgress: number,
  line?: string,
  explicitProgress?: number,
): number => {
  if (typeof explicitProgress === "number") {
    return Math.max(0, Math.min(100, explicitProgress));
  }

  if (line) {
    const pct = line.match(/(\d{1,3})\s?%/);
    if (pct?.[1]) {
      const value = Number(pct[1]);
      if (!Number.isNaN(value)) return Math.max(0, Math.min(100, value));
    }
  }

  if (status === "pending") return Math.max(existingProgress, 5);
  if (status === "running") return Math.max(existingProgress, 20);
  if (status === "success") return 100;
  if (status === "error") return Math.max(existingProgress, 100);
  if (status === "timeout") return Math.max(existingProgress, 100);
  if (status === "cancelled") return Math.max(existingProgress, 100);
  return existingProgress;
};

const interpreterToApi = (i: ScriptInterpreter): ScriptInterpreterTypeNumber =>
  (i === "powershell"
    ? ScriptInterpreterType.PowerShell
    : i === "cmd"
      ? ScriptInterpreterType.Cmd
      : ScriptInterpreterType.Bash) as ScriptInterpreterTypeNumber;

const appendLogLine = (existing: string[], line?: string) => {
  if (!line) return existing;
  const next = [...existing, line];
  if (next.length <= MAX_LOG_LINES_PER_ROW) return next;
  return next.slice(next.length - MAX_LOG_LINES_PER_ROW);
};

function payloadToTelemetryPatch(payload: ScriptOutputTelemetryPayload) {
  const taskId =
    payload.taskId != null && String(payload.taskId).length > 0
      ? String(payload.taskId)
      : undefined;
  const machineId =
    payload.machineId != null && String(payload.machineId).length > 0
      ? String(payload.machineId)
      : undefined;
  return {
    taskId,
    machineId,
    interpreter: payload.interpreter ?? undefined,
    stream: payload.stream ?? undefined,
    line: payload.line ?? undefined,
    message: undefined as string | undefined,
    status: undefined as string | undefined,
    progress: undefined as number | undefined,
  };
}

function parseTaskCompletedPayload(raw: unknown): ScriptTaskCompletedPayload | null {
  if (!raw || typeof raw !== "object") return null;
  const o = raw as Record<string, unknown>;
  const taskIdRaw = o.taskId ?? (o as { TaskId?: unknown }).TaskId;
  const machineIdRaw = o.machineId ?? (o as { MachineId?: unknown }).MachineId;
  const taskId = taskIdRaw != null ? String(taskIdRaw) : "";
  const machineId = machineIdRaw != null ? String(machineIdRaw) : "";
  if (!taskId || !machineId) return null;
  const exitRaw = o.exitCode ?? (o as { ExitCode?: unknown }).ExitCode;
  const exitCode = typeof exitRaw === "number" ? exitRaw : Number(exitRaw);
  const isSuccessRaw = o.isSuccess ?? (o as { IsSuccess?: unknown }).IsSuccess;
  const isSuccess = Boolean(isSuccessRaw);
  return {
    taskId,
    machineId,
    exitCode: Number.isFinite(exitCode) ? exitCode : -1,
    isSuccess,
  };
}

export function useMultiDeviceScriptRunner() {
  const [connectionState, setConnectionState] = useState<
    "connecting" | "connected" | "disconnected" | "error"
  >("connecting");
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [lastInvokeError, setLastInvokeError] = useState<string | null>(null);
  const [rowsByKey, setRowsByKey] = useState<Record<string, DeviceExecutionRow>>({});

  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const activeTaskByMachineRef = useRef<Map<string, string>>(new Map());
  /** Rows that received TaskCompleted — late stdout must not lower progress/status. */
  const terminalCompletionKeysRef = useRef<Set<string>>(new Set());

  const applyTaskCompleted = useCallback((payload: ScriptTaskCompletedPayload) => {
    const key = makeRowKey(payload.taskId, payload.machineId);
    terminalCompletionKeysRef.current.add(key);

    const success = payload.exitCode === 0;
    const logLine = `[system] Process exited with code ${payload.exitCode}.`;

    setRowsByKey((prev) => {
      const existing = prev[key];
      const status: ScriptExecutionStatus = success
        ? "success"
        : existing?.status === "cancelled"
          ? "cancelled"
          : "error";
      return {
        ...prev,
        [key]: {
          taskId: payload.taskId,
          machineId: payload.machineId,
          machineName: existing?.machineName ?? payload.machineId,
          status,
          progress: 100,
          logLines: appendLogLine(existing?.logLines ?? [], logLine),
          lastUpdatedAt: Date.now(),
          interpreter: existing?.interpreter,
        },
      };
    });
  }, []);

  const upsertTelemetry = useCallback(
    (incoming: {
      taskId?: string;
      machineId?: string;
      machineName?: string;
      interpreter?: string;
      stream?: string;
      line?: string;
      message?: string;
      status?: string;
      progress?: number;
    }) => {
      const machineId = incoming.machineId;
      if (!machineId) return;

      const taskId =
        incoming.taskId || activeTaskByMachineRef.current.get(machineId) || "unknown-task";

      const key = makeRowKey(taskId, machineId);
      const line = incoming.line || incoming.message || "";

      setRowsByKey((prev) => {
        const existing = prev[key];

        if (terminalCompletionKeysRef.current.has(key)) {
          if (!line) return prev;
          const base =
            existing ??
            ({
              taskId,
              machineId,
              machineName: machineId,
              status: "running" as const,
              progress: 100,
              logLines: [],
              lastUpdatedAt: Date.now(),
            } satisfies DeviceExecutionRow);
          return {
            ...prev,
            [key]: {
              ...base,
              logLines: appendLogLine(
                base.logLines,
                `[${incoming.stream || "stdout"}] ${line}`,
              ),
              lastUpdatedAt: Date.now(),
            },
          };
        }

        const normalizedFromStatus = normalizeStatus(incoming.status);

        let nextStatus: ScriptExecutionStatus =
          normalizedFromStatus ||
          inferStatusFromLine(line, incoming.stream) ||
          existing?.status ||
          "running";

        // Success + 100% is only applied via TaskCompleted, not stream heuristics.
        if (nextStatus === "success") {
          nextStatus = "running";
        }

        let nextProgress = inferProgress(
          nextStatus,
          existing?.progress ?? 0,
          line,
          incoming.progress,
        );

        if (nextStatus === "running" || nextStatus === "pending") {
          nextProgress = Math.min(MAX_INFERRED_PROGRESS, nextProgress);
        }

        return {
          ...prev,
          [key]: {
            taskId,
            machineId,
            machineName: incoming.machineName || existing?.machineName || machineId,
            status: nextStatus,
            progress: nextProgress,
            logLines: appendLogLine(
              existing?.logLines ?? [],
              line ? `[${incoming.stream || "stdout"}] ${line}` : undefined,
            ),
            lastUpdatedAt: Date.now(),
            interpreter: incoming.interpreter || existing?.interpreter,
          },
        };
      });
    },
    [],
  );

  useEffect(() => {
    const token = getToken();
    if (!token) {
      setConnectionState("error");
      setConnectionError("Missing auth token.");
      return;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}${SCRIPT_RUNNER_HUB_PATH}`, {
        accessTokenFactory: () => token || "",
      })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    connection.on(ScriptRunnerHubEvents.ScriptOutputTelemetry, (payload: ScriptOutputTelemetryPayload) => {
      const patch = payloadToTelemetryPatch(payload);
      upsertTelemetry(patch);
    });

    connection.on(ScriptRunnerHubEvents.TaskCompleted, (raw: unknown) => {
      const parsed = parseTaskCompletedPayload(raw);
      if (parsed) applyTaskCompleted(parsed);
    });

    connection.on(ScriptRunnerHubEvents.MachineStatusChanged, () => {
      /* reserved */
    });

    connection.onreconnecting(() => {
      setConnectionState("connecting");
    });

    connection.onreconnected(() => {
      setConnectionState("connected");
      setConnectionError(null);
    });

    connection.onclose((err) => {
      setConnectionState("disconnected");
      setConnectionError(err?.message || null);
    });

    connection
      .start()
      .then(() => {
        setConnectionState("connected");
        setConnectionError(null);
      })
      .catch((err: unknown) => {
        setConnectionState("error");
        setConnectionError(err instanceof Error ? err.message : "Failed to connect.");
      });

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
  }, [upsertTelemetry, applyTaskCompleted]);

  const subscribeToTask = useCallback(async (taskId: string) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;
    try {
      await connection.invoke("SubscribeToTask", taskId);
    } catch {
      // Non-fatal; telemetry may still arrive after reconnect.
    }
  }, []);

  const runOnDevices = useCallback(
    async (input: RunOnDevicesInput) => {
      setLastInvokeError(null);

      const timeoutSeconds = input.timeoutSeconds ?? 300;

      let taskId: string;
      try {
        const response = await executeScript({
          scriptContent: input.scriptContent,
          interpreterType: interpreterToApi(input.interpreter),
          targetMachineIds: input.machineIds,
          timeoutSeconds,
        });
        taskId = response.jobId;
        if (response.dispatchWarnings?.length) {
          setLastInvokeError(response.dispatchWarnings.join(" "));
        }
      } catch (err: unknown) {
        const msg = extractApiErrorMessage(err);
        setLastInvokeError(msg);
        throw new Error(msg);
      }

      const now = Date.now();

      for (const machineId of input.machineIds) {
        activeTaskByMachineRef.current.set(machineId, taskId);
      }

      setRowsByKey((prev) => {
        const next = { ...prev };
        for (const machineId of input.machineIds) {
          const rowKey = makeRowKey(taskId, machineId);
          terminalCompletionKeysRef.current.delete(rowKey);
          next[rowKey] = {
            taskId,
            machineId,
            machineName: input.machineNamesById[machineId] || machineId,
            status: "pending",
            progress: 5,
            logLines: [
              `[system] Queued script (${input.interpreter}) for ${input.machineNamesById[machineId] || machineId}.`,
            ],
            lastUpdatedAt: now,
            interpreter: input.interpreter,
          };
        }
        return next;
      });

      await subscribeToTask(taskId);

      setRowsByKey((prev) => {
        const next = { ...prev };
        for (const machineId of input.machineIds) {
          const rowKey = makeRowKey(taskId, machineId);
          const existing = next[rowKey];
          if (!existing) continue;
          next[rowKey] = {
            ...existing,
            status: "running",
            progress: Math.min(MAX_INFERRED_PROGRESS, Math.max(existing.progress, 15)),
            logLines: appendLogLine(existing.logLines, "[system] Execution dispatched to agent(s)."),
            lastUpdatedAt: Date.now(),
          };
        }
        return next;
      });

      return taskId;
    },
    [subscribeToTask],
  );

  const cancelMachine = useCallback(async (taskId: string, machineId: string) => {
    const rowKey = makeRowKey(taskId, machineId);
    terminalCompletionKeysRef.current.add(rowKey);
    setRowsByKey((prev) => {
      const row = prev[rowKey];
      if (!row) return prev;
      return {
        ...prev,
        [rowKey]: {
          ...row,
          status: "cancelled",
          progress: 100,
          logLines: appendLogLine(row.logLines, "[system] Cancel requested by operator."),
          lastUpdatedAt: Date.now(),
        },
      };
    });

    try {
      await cancelTask(taskId, machineId);
    } catch (err: unknown) {
      setLastInvokeError(extractApiErrorMessage(err));
    }
  }, []);

  const stopAll = useCallback(async () => {
    const taskIds = new Set<string>();
    Object.values(rowsByKey).forEach((row) => {
      if (row.status === "running" || row.status === "pending") {
        taskIds.add(row.taskId);
      }
    });

    setRowsByKey((prev) => {
      const next = { ...prev };
      Object.keys(next).forEach((key) => {
        const row = next[key];
        if (row.status === "running" || row.status === "pending") {
          terminalCompletionKeysRef.current.add(key);
          next[key] = {
            ...row,
            status: "cancelled",
            progress: 100,
            logLines: appendLogLine(row.logLines, "[system] Stopped by global control."),
            lastUpdatedAt: Date.now(),
          };
        }
      });
      return next;
    });

    for (const id of taskIds) {
      try {
        await cancelTask(id);
      } catch (err: unknown) {
        setLastInvokeError(extractApiErrorMessage(err));
      }
    }
  }, [rowsByKey]);

  const clearFinished = useCallback(() => {
    setRowsByKey((prev) => {
      const next: Record<string, DeviceExecutionRow> = {};
      Object.entries(prev).forEach(([key, value]) => {
        if (value.status === "pending" || value.status === "running") {
          next[key] = value;
        } else {
          terminalCompletionKeysRef.current.delete(key);
        }
      });
      return next;
    });
  }, []);

  const rows = useMemo(() => {
    return Object.values(rowsByKey).sort((a, b) => b.lastUpdatedAt - a.lastUpdatedAt);
  }, [rowsByKey]);

  return {
    rows,
    connectionState,
    connectionError,
    lastInvokeError,
    runOnDevices,
    cancelMachine,
    stopAll,
    clearFinished,
  };
}
