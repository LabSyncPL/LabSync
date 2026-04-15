export type SavedScriptInterpreter = "bash" | "powershell" | "cmd";

export interface SavedScript {
  id: string;
  title: string;
  description?: string | null;
  content: string;
  interpreter: SavedScriptInterpreter;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSavedScriptRequest {
  title: string;
  description?: string | null;
  content: string;
  interpreter: SavedScriptInterpreter;
}

export interface UpdateSavedScriptRequest extends CreateSavedScriptRequest {}
