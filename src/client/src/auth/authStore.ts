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
