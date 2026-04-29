import apiClient from "./axiosClient";
import type {
  ScheduledScriptDto,
  CreateScheduledScriptDto,
  UpdateScheduledScriptDto,
} from "../types/scheduledScripts";

export const scheduledScriptsQueryKey = ["scheduled-scripts"];

export const fetchScheduledScripts = async (): Promise<ScheduledScriptDto[]> => {
  const { data } = await apiClient.get<ScheduledScriptDto[]>("/api/scheduled-scripts");
  return data;
};

export const fetchScheduledScriptById = async (id: string): Promise<ScheduledScriptDto> => {
  const { data } = await apiClient.get<ScheduledScriptDto>(`/api/scheduled-scripts/${id}`);
  return data;
};

export const createScheduledScript = async (dto: CreateScheduledScriptDto): Promise<ScheduledScriptDto> => {
  const { data } = await apiClient.post<ScheduledScriptDto>("/api/scheduled-scripts", dto);
  return data;
};

export const updateScheduledScript = async (
  id: string,
  dto: UpdateScheduledScriptDto
): Promise<ScheduledScriptDto> => {
  const { data } = await apiClient.put<ScheduledScriptDto>(`/api/scheduled-scripts/${id}`, dto);
  return data;
};

export const deleteScheduledScript = async (id: string): Promise<void> => {
  await apiClient.delete(`/api/scheduled-scripts/${id}`);
};
