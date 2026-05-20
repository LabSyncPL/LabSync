import axios from "axios";
import { getToken, clearToken } from "../auth/authStore";
import { BASE_URL } from "../utils/baseUrl";

const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { "Content-Type": "application/json" },
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
      "Request failed. Please try again.";
    return Promise.reject({ ...error, message });
  },
);

export default apiClient;
