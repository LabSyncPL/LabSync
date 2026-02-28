export function DeviceAgentLogsCard() {
  return (
    <div className="bg-slate-800 rounded-2xl border border-slate-700 overflow-hidden flex flex-col h-full shadow-sm hover:shadow-md transition-shadow">
      <div className="px-6 py-5 border-b border-slate-700/50 flex items-center gap-3 bg-slate-800">
        <div className="p-2 bg-slate-700/30 rounded-lg text-slate-400 border border-slate-700/50">
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
        <h3 className="text-sm font-bold text-white uppercase tracking-wider">
          Agent Logs
        </h3>
      </div>

      <div className="flex-1 bg-slate-950 p-4 font-mono text-[11px] overflow-y-auto max-h-[520px] scrollbar-dark border-t border-slate-900 shadow-inner">
        <div className="space-y-1">
          <div className="flex gap-4 text-slate-500 border-b border-slate-800/50 pb-2 mb-3 px-2 font-medium uppercase tracking-wider text-[10px]">
            <span className="min-w-[140px]">Timestamp</span>
            <span className="min-w-[60px]">Level</span>
            <span>Message</span>
          </div>

          <div className="flex gap-4 group hover:bg-slate-900/50 px-2 py-1 rounded transition-colors border-l-2 border-transparent hover:border-l-primary-500/50">
            <span className="text-slate-500 min-w-[140px] shrink-0 font-mono">
              {new Date().toLocaleString()}
            </span>
            <span className="text-blue-400 font-bold min-w-[60px] shrink-0">
              [INFO]
            </span>
            <span className="text-slate-300">
              Device connected to SignalR Hub.
            </span>
          </div>

          <div className="flex gap-4 group hover:bg-slate-900/50 px-2 py-1 rounded transition-colors border-l-2 border-transparent hover:border-l-primary-500/50">
            <span className="text-slate-500 min-w-[140px] shrink-0 font-mono">
              {new Date(Date.now() - 1000 * 60 * 5).toLocaleString()}
            </span>
            <span className="text-blue-400 font-bold min-w-[60px] shrink-0">
              [INFO]
            </span>
            <span className="text-slate-300">
              System metrics collected successfully.
            </span>
          </div>

          <div className="flex gap-4 group hover:bg-slate-900/50 px-2 py-1 rounded transition-colors border-l-2 border-transparent hover:border-l-success/50">
            <span className="text-slate-500 min-w-[140px] shrink-0 font-mono">
              {new Date(Date.now() - 1000 * 60 * 60).toLocaleString()}
            </span>
            <span className="text-success font-bold min-w-[60px] shrink-0">
              [START]
            </span>
            <span className="text-slate-300">
              Agent service started (v1.0.2).
            </span>
          </div>

          <div className="flex gap-4 opacity-60 mt-6 px-2 py-1 border-t border-slate-800/30 pt-4">
            <span className="text-slate-600 min-w-[140px] shrink-0 font-mono">
              &gt;_
            </span>
            <span className="text-primary-400 animate-pulse font-medium">
              Listening for events...
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
