import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';

export function Layout() {
  return (
    <div className="flex h-screen overflow-hidden text-sm">
      <Sidebar />
      <main className="flex-1 flex flex-col bg-slate-900 overflow-hidden">
        <Outlet />
      </main>
    </div>
  );
}
