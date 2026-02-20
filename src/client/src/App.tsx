import { useState, useEffect } from 'react'
import './App.css'
import { getToken, clearToken } from './auth/authStore'
import { DeviceList } from './components/DeviceList'
import { Login } from './components/Login'

function App() {
  const [token, setTokenState] = useState<string | null>(() => getToken())

  useEffect(() => {
    const handleAuthChange = () => setTokenState(getToken())
    window.addEventListener('auth-change', handleAuthChange)
    return () => window.removeEventListener('auth-change', handleAuthChange)
  }, [])

  const handleLogout = () => clearToken()

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
      {token ? <DeviceList /> : <Login />}
    </div>
  )
}

export default App