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
} from "../types/scriptRunner";

const DEFAULT_BASE_URL = "http://localhost:5038";
const BASE_URL =
  (typeof import.meta !== "undefined" &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

export type ScriptInterpreter = "powershell" | "bash";
export type ScriptExecutionStatus =
  | "pending"
  | "running"
  | "success"
  | "error"
  | "timeout";

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
  if (value.includes("timeout") || value.includes("cancel")) return "timeout";
  if (value.includes("running") || value.includes("started")) return "running";
  if (value.includes("pending") || value.includes("queued")) return "pending";
  return null;
};

const inferStatusFromLine = (line: string, stream?: string): ScriptExecutionStatus | null => {
  const lower = line.toLowerCase();
  if (stream?.toLowerCase() === "stderr") return "error";
  if (lower.includes("error") || lower.includes("exception")) return "error";
  if (lower.includes("timed out") || lower.includes("timeout")) return "timeout";
  if (
    lower.includes("completed successfully") ||
    lower.includes("exit code: 0") ||
    lower.includes("finished successfully")
  ) {
    return "success";
  }
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
  return existingProgress;
};

const interpreterToApi = (i: ScriptInterpreter): ScriptInterpreterTypeNumber =>
  (i === "powershell" ? ScriptInterpreterType.PowerShell : ScriptInterpreterType.Bash) as ScriptInterpreterTypeNumber;

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

export function useMultiDeviceScriptRunner() {
  const [connectionState, setConnectionState] = useState<
    "connecting" | "connected" | "disconnected" | "error"
  >("connecting");
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [lastInvokeError, setLastInvokeError] = useState<string | null>(null);
  const [rowsByKey, setRowsByKey] = useState<Record<string, DeviceExecutionRow>>({});

  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const activeTaskByMachineRef = useRef<Map<string, string>>(new Map());

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

      const line = incoming.line || incoming.message || "";
      const normalizedFromStatus = normalizeStatus(incoming.status);

      setRowsByKey((prev) => {
        const key = makeRowKey(taskId, machineId);
        const existing = prev[key];
        const nextStatus =
          normalizedFromStatus ||
          inferStatusFromLine(line, incoming.stream) ||
          existing?.status ||
          "running";

        const nextProgress = inferProgress(
          nextStatus,
          existing?.progress ?? 0,
          line,
          incoming.progress,
        );

        return {
          ...prev,
          [key]: {
            taskId,
            machineId,
            machineName: incoming.machineName || existing?.machineName || machineId,
            status: nextStatus,
            progress: nextProgress,
            logLines: line
              ? [...(existing?.logLines ?? []), `[${incoming.stream || "stdout"}] ${line}`]
              : (existing?.logLines ?? []),
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

    connection.on(ScriptRunnerHubEvents.TaskCompleted, () => {
      /* reserved for future server events */
    });

    connection.on(ScriptRunnerHubEvents.MachineStatusChanged, () => {
      /* reserved for future server events */
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
  }, [upsertTelemetry]);

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
            progress: Math.max(existing.progress, 15),
            logLines: [...existing.logLines, "[system] Execution dispatched to agent(s)."],
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
    setRowsByKey((prev) => {
      const row = prev[rowKey];
      if (!row) return prev;
      return {
        ...prev,
        [rowKey]: {
          ...row,
          status: "timeout",
          progress: 100,
          logLines: [...row.logLines, "[system] Cancel requested by operator."],
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
          next[key] = {
            ...row,
            status: "timeout",
            progress: 100,
            logLines: [...row.logLines, "[system] Stopped by global control."],
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
