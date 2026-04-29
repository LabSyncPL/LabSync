export const ScheduledScriptTargetType = {
  SingleAgent: 0,
  Group: 1,
} as const;

export type ScheduledScriptTargetType = (typeof ScheduledScriptTargetType)[keyof typeof ScheduledScriptTargetType];

export interface CreateScheduledScriptDto {
  name: string;
  scriptContent: string;
  interpreterType: number;
  arguments: string[];
  timeoutSeconds: number;
  cronExpression?: string;
  runAt?: string;
  targetType: ScheduledScriptTargetType;
  targetId: string;
}

export interface UpdateScheduledScriptDto extends CreateScheduledScriptDto {
  isEnabled: boolean;
}

export interface ScheduledScriptDto {
  id: string;
  name: string;
  scriptContent: string;
  interpreterType: number;
  arguments: string[];
  timeoutSeconds: number;
  cronExpression?: string;
  runAt?: string;
  isEnabled: boolean;
  lastRunAt?: string;
  nextRunAt?: string;
  targetType: ScheduledScriptTargetType;
  targetId: string;
  createdAt: string;
}

export interface ScheduledScriptExecutionDto {
  id: string;
  scheduledScriptId: string;
  taskId: string;
  scheduledTime: string;
  status: string;
  startedAt?: string;
  finishedAt?: string;
  error?: string;
}
