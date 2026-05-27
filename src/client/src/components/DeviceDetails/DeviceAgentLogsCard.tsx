import { useEffect, useState } from "react";
import { fetchAgentLogs, type AgentLogEntry } from "../../api/devices";

const LEVEL_STYLES: Record<string, { badge: string; border: string }> = {
  INFO: {
    badge: "text-blue-600 dark:text-blue-400",
    border: "hover:border-l-blue-500/50",
  },
  WARN: {
    badge: "text-yellow-600 dark:text-yellow-400",
    border: "hover:border-l-yellow-500/50",
  },
  ERROR: {
    badge: "text-red-600 dark:text-red-400",
    border: "hover:border-l-red-500/50",
  },
  CRITICAL: {
    badge: "text-red-700 dark:text-red-600",
    border: "hover:border-l-red-600/50",
  },
};

function levelStyle(level: string) {
  return (
    LEVEL_STYLES[level.toUpperCase()] ?? {
      badge: "text-slate-500 dark:text-slate-400",
      border: "",
    }
  );
}

interface Props {
  deviceId: string;
}

export function DeviceAgentLogsCard({ deviceId }: Props) {
  const [logs, setLogs] = useState<AgentLogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError(null);
        const data = await fetchAgentLogs(deviceId);
        if (!cancelled) setLogs(data.slice().reverse()); // najnowsze na górze
      } catch {
        if (!cancelled) setError("Failed to load agent logs.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    const interval = setInterval(load, 15_000); // odświeżaj co 15 s
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [deviceId]);

  return (
    <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 overflow-hidden flex flex-col h-full shadow-sm hover:shadow-md transition-shadow">
      <div className="px-6 py-5 border-b border-slate-100 dark:border-slate-700/50 flex items-center gap-3 bg-white dark:bg-slate-800">
        <div className="p-2 bg-slate-100 dark:bg-slate-700/30 rounded-lg text-slate-500 dark:text-slate-400 border border-slate-200 dark:border-slate-700/50">
          <svg
            className="w-5 h-5"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
            />
          </svg>
        </div>
        <h3 className="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider">
          Agent Logs
        </h3>
      </div>

      <div className="flex-1 bg-slate-50 dark:bg-slate-950 p-4 font-mono text-[11px] overflow-y-auto max-h-[520px] scrollbar-light dark:scrollbar-dark border-t border-slate-100 dark:border-slate-900 shadow-inner">
        <div className="space-y-1">
          <div className="flex gap-4 text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800/50 pb-2 mb-3 px-2 font-medium uppercase tracking-wider text-[10px]">
            <span className="min-w-[140px]">Timestamp</span>
            <span className="min-w-[60px]">Level</span>
            <span>Message</span>
          </div>

          {loading && (
            <div className="px-2 py-4 text-slate-500 text-center">
              Loading logs...
            </div>
          )}

          {error && (
            <div className="px-2 py-4 text-danger text-center">{error}</div>
          )}

          {!loading && !error && logs.length === 0 && (
            <div className="px-2 py-4 text-slate-600 dark:text-slate-600 text-center">
              No logs yet.
            </div>
          )}

          {logs.map((log, i) => {
            const { badge, border } = levelStyle(log.level);
            return (
              <div
                key={i}
                className={`flex gap-4 group hover:bg-slate-200/50 dark:hover:bg-slate-900/50 px-2 py-1 rounded transition-colors border-l-2 border-transparent ${border}`}
              >
                <span className="text-slate-500 dark:text-slate-500 min-w-[140px] shrink-0 font-mono">
                  {new Date(log.timestamp).toLocaleString()}
                </span>
                <span className={`${badge} font-bold min-w-[60px] shrink-0`}>
                  [{log.level}]
                </span>
                <span className="text-slate-700 dark:text-slate-300 break-all">
                  {log.message}
                </span>
              </div>
            );
          })}

          <div className="flex gap-4 opacity-60 mt-6 px-2 py-1 border-t border-slate-200 dark:border-slate-800/30 pt-4">
            <span className="text-slate-400 dark:text-slate-600 min-w-[140px] shrink-0 font-mono">
              &gt;_
            </span>
            <span className="text-primary-600 dark:text-primary-400 animate-pulse font-medium">
              Listening for events...
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
