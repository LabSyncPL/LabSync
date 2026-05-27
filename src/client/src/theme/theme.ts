export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'labsync-theme-mode';

function prefersDark() {
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export function getStoredThemeMode(): ThemeMode {
  const stored = window.localStorage.getItem(STORAGE_KEY);
  if (stored === 'light' || stored === 'dark' || stored === 'system') {
    return stored;
  }
  return 'system';
}

export function resolveTheme(mode: ThemeMode): 'light' | 'dark' {
  if (mode === 'system') {
    return prefersDark() ? 'dark' : 'light';
  }
  return mode;
}

export function applyTheme(mode: ThemeMode) {
  const resolved = resolveTheme(mode);
  const root = document.documentElement;
  root.classList.toggle('dark', resolved === 'dark');
}

export function setThemeMode(mode: ThemeMode) {
  window.localStorage.setItem(STORAGE_KEY, mode);
  applyTheme(mode);
  window.dispatchEvent(new CustomEvent('theme-change'));
}
