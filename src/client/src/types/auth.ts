/**
 * Mirrors LabSync.Core.Dto.LoginRequest
 */
export interface LoginRequest {
  username: string;
  password: string;
}

/**
 * Mirrors LabSync.Core.Dto.LoginResponse
 */
export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
}
