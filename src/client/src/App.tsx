import { useState, useEffect, useCallback } from "react";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { getToken } from "./auth/authStore";
import { getSystemStatus } from "./api/system";
import { Layout } from "./components/Layout/Layout";
import { AuthPage } from "./components/AuthPage";
import { SetupWizard } from "./components/SetupWizard";
import { Dashboard } from "./pages/Dashboard";
import { DeviceDetails } from "./pages/DeviceDetails";
import { TasksPage } from "./pages/TasksPage";
import { RemoteViewPage } from "./pages/RemoteViewPage";
import { SettingsPage } from "./pages/SettingsPage";
import { ScriptDeploymentDashboard } from "./components/ScriptDeploymentDashboard";
import { applyTheme, getStoredThemeMode } from "./theme/theme";

function AuthBrandLogo() {
  return (
    <div className="mb-8 flex justify-center">
      <img
        src="/LabSyncLogoH.svg"
        alt="LabSync"
        className="h-10 w-auto max-w-full object-contain"
      />
    </div>
  );
}

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
    const syncTheme = () => applyTheme(getStoredThemeMode());
    syncTheme();
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onSystemThemeChange = () => {
      if (getStoredThemeMode() === "system") {
        syncTheme();
      }
    };
    media.addEventListener("change", onSystemThemeChange);
    window.addEventListener("theme-change", syncTheme);
    return () => {
      media.removeEventListener("change", onSystemThemeChange);
      window.removeEventListener("theme-change", syncTheme);
    };
  }, []);

  useEffect(() => {
    const handleAuthChange = () => setTokenState(getToken());
    window.addEventListener("auth-change", handleAuthChange);
    return () => window.removeEventListener("auth-change", handleAuthChange);
  }, []);

  if (statusLoading) {
    return (
      <div className="flex items-center justify-center h-screen bg-slate-100 dark:bg-slate-900">
        <p className="text-slate-600 dark:text-slate-400">Loading…</p>
      </div>
    );
  }

  if (setupComplete === false) {
    return (
      <div className="min-h-screen bg-slate-100 dark:bg-slate-900 flex items-center justify-center p-4">
        <div className="w-full max-w-md">
          <AuthBrandLogo />
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
              <div className="min-h-screen bg-slate-100 dark:bg-slate-900 flex items-center justify-center p-4">
                <div className="w-full max-w-md">
                  <AuthBrandLogo />
                  <AuthPage onSetupRequired={loadSystemStatus} />
                </div>
              </div>
            )
          }
        />
        <Route
          path="/*"
          element={token ? <Layout /> : <Navigate to="/login" replace />}
        >
          <Route index element={<Dashboard />} />
          <Route path="devices/:id" element={<DeviceDetails />} />
          <Route path="tasks" element={<TasksPage />} />
          <Route path="vnc" element={<RemoteViewPage />} />
          <Route
            path="scripts"
            element={<ScriptDeploymentDashboard />}
          />
          <Route path="settings" element={<SettingsPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
