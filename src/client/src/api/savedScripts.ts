import apiClient from "./axiosClient";
import type {
  CreateSavedScriptRequest,
  SavedScript,
  UpdateSavedScriptRequest,
} from "../types/savedScripts";

const SAVED_SCRIPTS_API_PREFIX = "/api/saved-scripts";

export const savedScriptsQueryKey = ["saved-scripts"] as const;

export async function fetchSavedScripts(): Promise<SavedScript[]> {
  const { data } = await apiClient.get<SavedScript[]>(SAVED_SCRIPTS_API_PREFIX);
  return data;
}

export async function createSavedScript(payload: CreateSavedScriptRequest): Promise<SavedScript> {
  const { data } = await apiClient.post<SavedScript>(SAVED_SCRIPTS_API_PREFIX, payload);
  return data;
}

export async function updateSavedScript(
  id: string,
  payload: UpdateSavedScriptRequest,
): Promise<SavedScript> {
  const { data } = await apiClient.put<SavedScript>(`${SAVED_SCRIPTS_API_PREFIX}/${id}`, payload);
  return data;
}

export async function deleteSavedScript(id: string): Promise<void> {
  await apiClient.delete(`${SAVED_SCRIPTS_API_PREFIX}/${id}`);
}
