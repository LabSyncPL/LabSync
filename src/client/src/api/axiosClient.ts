import axios from 'axios';
import { getToken, clearToken } from '../auth/authStore';

const DEFAULT_BASE_URL = 'http://localhost:5038';
const BASE_URL =
  (typeof import.meta !== 'undefined' &&
    (import.meta as any)?.env?.VITE_API_BASE_URL) ||
  DEFAULT_BASE_URL;

const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
});

apiClient.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    const headers = config.headers ?? {};
    headers.Authorization = `Bearer ${token}`;
    config.headers = headers;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      clearToken();
    }
    const message =
      error.response?.data?.message ||
      error.message ||
      'Request failed. Please try again.';
    return Promise.reject({ ...error, message });
  }
);

export default apiClient;
