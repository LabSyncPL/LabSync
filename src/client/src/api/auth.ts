import apiClient from './axiosClient';
import type { LoginRequest, LoginResponse } from '../types/auth';

export async function login(credentials: LoginRequest): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/api/auth/login', credentials);
  return data;
}
