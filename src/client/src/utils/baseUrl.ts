const envBaseUrl = import.meta.env.VITE_API_BASE_URL;
const DEFAULT_BASE_URL = "http://localhost:5038";

export const BASE_URL =
  typeof envBaseUrl === "string"
    ? envBaseUrl.replace(/\/$/, "")
    : DEFAULT_BASE_URL;
