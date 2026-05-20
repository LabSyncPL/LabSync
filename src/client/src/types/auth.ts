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
  expiresInSeconds: number;
  tokenType: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
}

export interface AccountProfile {
  username: string;
  createdAt: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangeUsernameRequest {
  newUsername: string;
  currentPassword: string;
}
