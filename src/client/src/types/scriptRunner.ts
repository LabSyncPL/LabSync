/**
 * Mirrors LabSync.Core.Dto.ScriptInterpreterType / LabSync.Modules.ScriptExecutor.Models.InterpreterType
 */
export const ScriptInterpreterType = {
  PowerShell: 0,
  Bash: 1,
  Cmd: 2,
} as const;

export type ScriptInterpreterTypeNumber =
  (typeof ScriptInterpreterType)[keyof typeof ScriptInterpreterType];

/**
 * Mirrors LabSync.Core.Dto.ExecuteScriptRequest (JSON camelCase).
 */
export interface ScriptRequest {
  scriptContent: string;
  interpreterType: ScriptInterpreterTypeNumber;
  targetMachineIds: string[];
  timeoutSeconds: number;
  arguments?: string[] | null;
}

/**
 * Mirrors LabSync.Core.Dto.ExecuteScriptResponse.
 * `jobId` is the logical task id used for SignalR groups and cancellation.
 */
export interface CommandResponse {
  jobId: string;
  dispatchWarnings?: string[] | null;
}

/**
 * Optional structured status events (forward-looking; use ScriptRunnerHubEvents).
 */
export interface MachineStatusUpdate {
  taskId: string;
  machineId: string;
  machineName?: string;
  status: "pending" | "running" | "success" | "error" | "timeout" | "cancelled";
  progress?: number;
  message?: string;
}

/**
 * Mirrors LabSync.Core.Dto.ScriptOutputTelemetryDto (SignalR JSON camelCase).
 */
export interface ScriptOutputTelemetryPayload {
  taskId?: string | null;
  machineId?: string | null;
  interpreter?: string | null;
  stream?: string | null;
  line?: string | null;
  timestampUtc?: string | null;
}

/**
 * Mirrors LabSync.Core.Dto.ScriptTaskCompletedDto (SignalR JSON camelCase).
 */
export interface ScriptTaskCompletedPayload {
  taskId: string;
  machineId: string;
  exitCode: number;
  isSuccess: boolean;
}
