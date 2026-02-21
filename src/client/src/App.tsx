import { useState, useEffect, useCallback } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { getToken } from './auth/authStore';
import { getSystemStatus } from './api/system';
import { Layout } from './components/Layout/Layout';
import { Login } from './components/Login';
import { SetupWizard } from './components/SetupWizard';
import { Dashboard } from './pages/Dashboard';
import { DeviceDetails } from './pages/DeviceDetails';
import { TasksPage } from './pages/TasksPage';
import { PlaceholderPage } from './pages/PlaceholderPage';
import { SettingsPage } from './pages/SettingsPage';

function App() {
  const [token, setTokenState] = useState<string | null>(() => getToken());
  const [statusLoading, setStatusLoading] = useState(true);
  const [setupComplete, setSetupComplete] = useState<boolean | null>(null);

  const loadSystemStatus = useCallback(async () => {
    try {
      const status = await getSystemStatus();
      setSetupComplete(status.setupComplete);
    } catch {
      setSetupComplete(true);
    } finally {
      setStatusLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSystemStatus();
  }, [loadSystemStatus]);

  useEffect(() => {
    const handleAuthChange = () => setTokenState(getToken());
    window.addEventListener('auth-change', handleAuthChange);
    return () => window.removeEventListener('auth-change', handleAuthChange);
  }, []);

  if (statusLoading) {
    return (
      <div className="flex items-center justify-center h-screen bg-slate-900">
        <p className="text-slate-400">Loading…</p>
      </div>
    );
  }

  if (setupComplete === false) {
    return (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center p-4">
        <div className="w-full max-w-md">
          <div className="mb-8 text-center">
            <div className="w-12 h-12 bg-primary-600 rounded-lg flex items-center justify-center font-bold text-white text-xl mx-auto mb-4">
              LS
            </div>
            <h1 className="text-2xl font-bold text-white">LabSync</h1>
          </div>
          <SetupWizard onSetupComplete={loadSystemStatus} />
        </div>
      </div>
    );
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route
          path="/login"
          element={
            token ? (
              <Navigate to="/" replace />
            ) : (
              <div className="min-h-screen bg-slate-900 flex items-center justify-center p-4">
                <div className="w-full max-w-md">
                  <div className="mb-8 text-center">
                    <div className="w-12 h-12 bg-primary-600 rounded-lg flex items-center justify-center font-bold text-white text-xl mx-auto mb-4">
                      LS
                    </div>
                    <h1 className="text-2xl font-bold text-white">LabSync</h1>
                  </div>
                  <Login onSetupRequired={loadSystemStatus} />
                </div>
              </div>
            )
          }
        />
        <Route
          path="/*"
          element={
            token ? (
              <Layout />
            ) : (
              <Navigate to="/login" replace />
            )
          }
        >
          <Route index element={<Dashboard />} />
          <Route path="devices/:id" element={<DeviceDetails />} />
          <Route path="tasks" element={<TasksPage />} />
          <Route path="vnc" element={<PlaceholderPage title="Remote View" />} />
          <Route path="scripts" element={<PlaceholderPage title="Task Runner" />} />
          <Route path="repository" element={<PlaceholderPage title="Repository" />} />
          <Route path="audit" element={<PlaceholderPage title="Audit Log" />} />
          <Route path="settings" element={<SettingsPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
