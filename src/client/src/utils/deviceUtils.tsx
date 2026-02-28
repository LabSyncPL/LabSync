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
          <path d="M12 0c-6.627 0-12 5.373-12 12s5.373 12 12 12 12-5.373 12-12-5.373-12-12-12zm4.333 3.667c.736 0 1.333.597 1.333 1.333 0 .736-.597 1.333-1.333 1.333-.736 0-1.333-.597-1.333-1.333 0-.736.597-1.333 1.333-1.333zm-8.667 0c.736 0 1.333.597 1.333 1.333 0 .736-.597 1.333-1.333 1.333-.736 0-1.333-.597-1.333-1.333 0-.736.597-1.333 1.333-1.333zm9.056 12.333h-1.333v2.667h-2.667v-2.667h-1.333v2.667h-2.667v-2.667h-1.389v-4h10.778v4z" />
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
