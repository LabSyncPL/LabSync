import { useState, useEffect, useCallback } from 'react'
import './App.css'
import { getToken, clearToken } from './auth/authStore'
import { getSystemStatus } from './api/system'
import { DeviceList } from './components/DeviceList'
import { Login } from './components/Login'
import { SetupWizard } from './components/SetupWizard'

function App() {
  const [token, setTokenState] = useState<string | null>(() => getToken())
  const [statusLoading, setStatusLoading] = useState(true)
  const [setupComplete, setSetupComplete] = useState<boolean | null>(null)

  const loadSystemStatus = useCallback(async () => {
    try {
      const status = await getSystemStatus()
      setSetupComplete(status.setupComplete)
    } catch {
      setSetupComplete(true)
    } finally {
      setStatusLoading(false)
    }
  }, [])

  useEffect(() => {
    loadSystemStatus()
  }, [loadSystemStatus])

  useEffect(() => {
    const handleAuthChange = () => setTokenState(getToken())
    window.addEventListener('auth-change', handleAuthChange)
    return () => window.removeEventListener('auth-change', handleAuthChange)
  }, [])

  const handleLogout = () => clearToken()

  if (statusLoading) {
    return (
      <div className="App">
        <p style={{ padding: '2rem', textAlign: 'center' }}>Loading…</p>
      </div>
    )
  }

  if (setupComplete === false) {
    return (
      <div className="App">
        <header style={{ marginBottom: '1rem' }}>
          <h1 style={{ margin: 0 }}>LabSync</h1>
        </header>
        <SetupWizard onSetupComplete={loadSystemStatus} />
      </div>
    )
  }

  return (
    <div className="App">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
        <h1 style={{ margin: 0 }}>Admin Panel</h1>
        {token && (
          <button type="button" onClick={handleLogout} style={{ padding: '0.4rem 0.8rem', cursor: 'pointer' }}>
            Log out
          </button>
        )}
      </header>
      {token ? <DeviceList /> : <Login onSetupRequired={loadSystemStatus} />}
    </div>
  )
}

export default App
