interface PlaceholderPageProps {
  title: string;
}

export function PlaceholderPage({ title }: PlaceholderPageProps) {
  return (
    <>
      <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-900 shrink-0">
        <h1 className="text-xl font-semibold text-white">{title}</h1>
      </header>
      <div className="flex-1 overflow-auto p-8">
        <div className="max-w-2xl mx-auto text-center py-16">
          <div className="w-20 h-20 bg-slate-800 rounded-xl flex items-center justify-center mx-auto mb-6">
            <svg className="w-10 h-10 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6"></path>
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-white mb-3">{title}</h2>
          <p className="text-slate-400 mb-8">
            This feature is coming soon. The backend infrastructure is being prepared.
          </p>
          <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 text-left">
            <h3 className="text-white font-semibold mb-3">Planned Features:</h3>
            <ul className="space-y-2 text-slate-300 text-sm">
              <li className="flex items-start">
                <span className="text-primary-500 mr-2">•</span>
                <span>Full implementation of {title.toLowerCase()} functionality</span>
              </li>
              <li className="flex items-start">
                <span className="text-primary-500 mr-2">•</span>
                <span>Integration with backend APIs</span>
              </li>
              <li className="flex items-start">
                <span className="text-primary-500 mr-2">•</span>
                <span>Real-time updates and monitoring</span>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </>
  );
}
