export function formatLastSeen(value: string | null): string {
  if (value == null) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins} min ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  return date.toLocaleDateString();
}

export function getPlatformIcon(platform: number, className = "w-4 h-4") {
  switch (platform) {
    case 1: // Windows
      return (
        <svg className={className} viewBox="0 0 24 24" fill="currentColor">
          <path d="M0 3.449L9.75 2.1v9.451H0m10.949-9.602L24 0v11.4h-13.051M0 12.6h9.75v9.451L0 20.699M10.949 12.6H24V24l-12.9-1.801" />
        </svg>
      );
    case 2: // Linux
      return (
        <svg className={className} viewBox="0 0 24 24" fill="currentColor">
          <path d="M11.992 0C10.707 0 9.444.252 8.355.727c-1.898.835-3.235 2.535-3.235 4.743 0 1.343.463 2.573 1.233 3.567-1.222.863-2.022 2.26-2.022 3.847 0 1.587.8 2.984 2.022 3.847-.77 1-1.233 2.224-1.233 3.567 0 2.208 1.337 3.908 3.235 4.743 1.089.475 2.352.727 3.637.727 1.285 0 2.548-.252 3.637-.727 1.898-.835 3.235-2.535 3.235-4.743 0-1.343-.463-2.567-1.233-3.567 1.222-.863 2.022-2.26 2.022-3.847 0-1.587-.8-2.984-2.022-3.847.77-1 1.233-2.224 1.233-3.567 0-2.208-1.337-3.908-3.235-4.743C14.54.252 13.277 0 11.992 0zm-2.016 4.54a.972.972 0 1 1 0 1.944.972.972 0 0 1 0-1.944zm4.032 0a.972.972 0 1 1 0 1.944.972.972 0 0 1 0-1.944zM12 7.824a1.644 1.644 0 1 1 0 3.288 1.644 1.644 0 0 1 0-3.288zM6.92 13.064h1.008c.616 0 1.112.496 1.112 1.112v2.016c0 .616-.496 1.112-1.112 1.112H6.92c-.616 0-1.112-.496-1.112-1.112v-2.016c0-.616.496-1.112 1.112-1.112zm10.16 0h1.008c.616 0 1.112.496 1.112 1.112v2.016c0 .616-.496 1.112-1.112 1.112h-1.008c-.616 0-1.112-.496-1.112-1.112v-2.016c0-.616.496-1.112 1.112-1.112zm-6.104 5.28h4.048c.616 0 1.112.496 1.112 1.112v1.008c0 .616-.496 1.112-1.112 1.112H10.976c-.616 0-1.112-.496-1.112-1.112v-1.008c0-.616.496-1.112 1.112-1.112z" />
        </svg>
      );
    default:
      return null;
  }
}

export function formatBytesPerSecond(value: number): string {
  if (value <= 0) return "0 B/s";
  const kb = value / 1024;
  if (kb < 1024) return `${kb.toFixed(1)} KB/s`;
  const mb = kb / 1024;
  if (mb < 1024) return `${mb.toFixed(1)} MB/s`;
  const gb = mb / 1024;
  return `${gb.toFixed(1)} GB/s`;
}
