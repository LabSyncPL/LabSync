export function TasksPage() {

  return (
    <>
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <div>
          <h1 className="text-xl font-semibold text-white">Tasks & Deployments</h1>
          <p className="text-slate-500 text-xs">Monitor execution status and view logs</p>
        </div>
        <div className="flex items-center gap-3">
          <button className="bg-primary-600 hover:bg-primary-500 text-white px-4 py-2 rounded-lg font-medium text-xs shadow-lg shadow-primary-500/20 flex items-center transition-colors">
            <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6"></path>
            </svg>
            New Task
          </button>
        </div>
      </header>

      <div className="px-8 py-4 border-b border-slate-800 bg-slate-900 shrink-0">
        <div className="bg-slate-900/70 rounded-xl px-4 py-3 flex flex-wrap gap-4 items-center">
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <svg className="h-4 w-4 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
            </div>
            <input
              type="text"
              placeholder="Search tasks..."
              className="pl-10 pr-4 py-2 bg-slate-800 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-primary-500 w-64"
            />
          </div>
          <div className="h-6 w-px bg-slate-700 mx-2"></div>
          <select className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg focus:ring-primary-500 focus:border-primary-500 block p-2">
            <option>All Status</option>
            <option>Running</option>
            <option>Success</option>
            <option>Failed</option>
            <option>Pending</option>
          </select>
          <select className="bg-slate-800 border border-slate-700 text-white text-xs rounded-lg focus:ring-primary-500 focus:border-primary-500 block p-2">
            <option>All Types</option>
            <option>Package Installation</option>
            <option>Script Execution</option>
            <option>System Update</option>
          </select>
          <div className="flex-1"></div>
          <span className="text-xs text-slate-500">Last 7 days</span>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-8">
        <div className="text-center py-16">
          <div className="w-16 h-16 bg-slate-800 rounded-xl flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"></path>
            </svg>
          </div>
          <h3 className="text-lg font-semibold text-white mb-2">No tasks yet</h3>
          <p className="text-slate-400 text-sm mb-6">
            Tasks will appear here once you create jobs for devices.
          </p>
          <button className="bg-primary-600 hover:bg-primary-500 text-white px-4 py-2 rounded-lg font-medium text-sm">
            Create First Task
          </button>
        </div>
      </div>
    </>
  );
}
