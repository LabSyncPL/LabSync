const TOKEN_KEY = 'labsync_token';

let token: string | null = null;

function notifyAuthChange(): void {
  window.dispatchEvent(new Event('auth-change'));
}

export function getToken(): string | null {
  if (token !== null) return token;
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(accessToken: string): void {
  token = accessToken;
  localStorage.setItem(TOKEN_KEY, accessToken);
  notifyAuthChange();
}

export function clearToken(): void {
  token = null;
  localStorage.removeItem(TOKEN_KEY);
  notifyAuthChange();
}

export function isAuthenticated(): boolean {
  return getToken() !== null;
}

export function getAdminUsername(): string | null {
  const accessToken = getToken();
  if (!accessToken) return null;

  const parts = accessToken.split('.');
  if (parts.length !== 3) return null;

  const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');

  try {
    const payload = JSON.parse(atob(padded)) as {
      sub?: string;
      unique_name?: string;
    };
    return payload.unique_name ?? payload.sub ?? null;
  } catch {
    return null;
  }
}
