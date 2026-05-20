import apiClient from './axiosClient';
import type {
  AccountProfile,
  ChangePasswordRequest,
  ChangeUsernameRequest,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
} from '../types/auth';

export async function login(credentials: LoginRequest): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/api/auth/login', credentials);
  return data;
}

export async function registerAccount(payload: RegisterRequest): Promise<{ message: string }> {
  const { data } = await apiClient.post<{ message: string }>('/api/auth/register', payload);
  return data;
}

export async function fetchAccountProfile(): Promise<AccountProfile> {
  const { data } = await apiClient.get<AccountProfile>('/api/account/me');
  return data;
}

export async function changePassword(
  payload: ChangePasswordRequest,
): Promise<LoginResponse> {
  const { data } = await apiClient.patch<LoginResponse>('/api/account/password', payload);
  return data;
}

export async function changeUsername(
  payload: ChangeUsernameRequest,
): Promise<LoginResponse> {
  const { data } = await apiClient.patch<LoginResponse>('/api/account/username', payload);
  return data;
}
