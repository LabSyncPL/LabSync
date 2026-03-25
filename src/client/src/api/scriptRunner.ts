import axios, { type AxiosError } from "axios";
import apiClient from "./axiosClient";
import type {
  CommandResponse,
  ScriptRequest,
} from "../types/scriptRunner";

export const SCRIPT_RUNNER_API_PREFIX = "/api/script-runner";

/** Admin hub for script telemetry (JWT). Must match server: MapHub<ScriptHub>("/scriptHub"). */
export const SCRIPT_RUNNER_HUB_PATH = "/scriptHub";

export const ScriptRunnerHubEvents = {
  ScriptOutputTelemetry: "ScriptOutputTelemetry",
  TaskCompleted: "TaskCompleted",
  MachineStatusChanged: "MachineStatusChanged",
} as const;

export type ScriptRunnerHubEventName =
  (typeof ScriptRunnerHubEvents)[keyof typeof ScriptRunnerHubEvents];

/**
 * Extracts a user-facing message from axios errors, ASP.NET ProblemDetails, or ApiResponse bodies.
 */
export function extractApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const ax = error as AxiosError<unknown>;
    const status = ax.response?.status;
    if (status === 401) return "Unauthorized. Please sign in again.";
    if (status === 403) return "Forbidden.";
    if (status === 404) return "Not found.";

    const data = ax.response?.data;
    if (typeof data === "string" && data.trim()) return data;

    if (data && typeof data === "object") {
      const d = data as Record<string, unknown>;
      if (typeof d.message === "string" && d.message.trim()) return d.message;
      if (typeof d.detail === "string" && d.detail.trim()) return d.detail;
      if (typeof d.title === "string" && d.title.trim()) {
        const detail =
          typeof d.detail === "string" && d.detail.trim() ? `: ${d.detail}` : "";
        return `${d.title}${detail}`;
      }
      const errors = d.errors;
      if (errors && typeof errors === "object") {
        const first = Object.values(errors as Record<string, unknown>).flat()[0];
        if (typeof first === "string") return first;
      }
    }

    const msg = ax.message?.trim();
    if (msg) return msg;
  }

  if (
    error &&
    typeof error === "object" &&
    "message" in error &&
    typeof (error as { message: unknown }).message === "string"
  ) {
    const m = (error as { message: string }).message.trim();
    if (m) return m;
  }

  if (error instanceof Error && error.message) return error.message;
  return "Request failed.";
}

export async function executeScript(
  payload: ScriptRequest,
): Promise<CommandResponse> {
  const { data } = await apiClient.post<CommandResponse>(
    `${SCRIPT_RUNNER_API_PREFIX}/execute`,
    payload,
  );
  return data;
}

export async function cancelTask(
  taskId: string,
  machineId?: string,
): Promise<void> {
  await apiClient.post(`${SCRIPT_RUNNER_API_PREFIX}/cancel`, {
    taskId,
    machineId: machineId ?? null,
  });
}
