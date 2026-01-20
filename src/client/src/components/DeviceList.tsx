import { useEffect, useState } from 'react';
import apiClient from '../api/axiosClient';

interface Device {
  id: string;
  hostname: string;
  macAddress: string;
  ipAddress: string;
  osVersion: string;
  status: number; 
  lastSeenAt: string;
  isApproved: boolean; 
}

export const DeviceList = () => {
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    fetchDevices();
  }, []);

  const fetchDevices = () => {
    apiClient.get<Device[]>('/api/devices')
      .then(response => {
        setDevices(response.data);
        setLoading(false);
      })
      .catch(err => {
        console.error(err);
        setError('Nie udało się połączyć z serwerem.');
        setLoading(false);
      });
  };

  const handleApprove = async (deviceId: string) => {
    try {
      await apiClient.post(`/api/devices/${deviceId}/approve`);
      setDevices(currentDevices => 
        currentDevices.map(device => 
          device.id === deviceId 
            ? { ...device, isApproved: true, status: 1 }
            : device
        )
      );
    } catch (err) {
      console.error("Błąd zatwierdzania:", err);
      alert("Nie udało się zatwierdzić urządzenia.");
    }
  };

  if (loading) return <p>Ładowanie danych...</p>;
  if (error) return <p style={{ color: 'red' }}>{error}</p>;

  return (
    <div className="container">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2>Lista Urządzeń (LabSync)</h2>
        <button onClick={fetchDevices}>Odśwież</button>
      </div>

      <table border={1} cellPadding={10} style={{ width: '100%', borderCollapse: 'collapse', marginTop: '20px' }}>
        <thead>
          <tr style={{ background: '#333', color: '#fff' }}>
            <th>Hostname</th>
            <th>Adres MAC</th>
            <th>IP</th>
            <th>System</th>
            <th>Ostatnio widziany</th>
            <th>Status</th>
            <th>Akcje</th> 
          </tr>
        </thead>
        <tbody>
          {devices.map(device => (
            <tr key={device.id} style={{ background: device.isApproved ? 'white' : '#fff8e1' }}>
              <td><strong>{device.hostname}</strong></td>
              <td style={{ fontFamily: 'monospace' }}>{device.macAddress}</td>
              <td>{device.ipAddress || '-'}</td>
              <td>{device.osVersion}</td>
              <td>{new Date(device.lastSeenAt).toLocaleString()}</td>
              <td>
                {!device.isApproved ? (
                   <span style={{ color: '#d35400', fontWeight: 'bold' }}>⏳ Oczekuje</span>
                ) : device.status === 1 ? (
                   <span style={{ color: 'green', fontWeight: 'bold' }}>🟢 Online</span>
                ) : (
                   <span style={{ color: 'red' }}>🔴 Offline</span>
                )}
              </td>
              
              <td style={{ textAlign: 'center' }}>
                {!device.isApproved ? (
                  <button 
                    onClick={() => handleApprove(device.id)}
                    style={{
                      backgroundColor: '#2ecc71',
                      color: 'white',
                      border: 'none',
                      padding: '8px 12px',
                      borderRadius: '4px',
                      cursor: 'pointer',
                      fontWeight: 'bold'
                    }}
                  >
                    Zatwierdź
                  </button>
                ) : (
                  <span style={{ color: '#95a5a6', fontSize: '0.9em' }}>Zatwierdzono</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};