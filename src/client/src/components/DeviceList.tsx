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
}

export const DeviceList = () => {
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
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
  }, []);

  if (loading) return <p>Ładowanie danych...</p>;
  if (error) return <p style={{ color: 'red' }}>{error}</p>;

  return (
    <div className="container">
      <h2>Lista Urządzeń (LabSync)</h2>
      <table border={1} cellPadding={10} style={{ width: '100%', borderCollapse: 'collapse', marginTop: '20px' }}>
        <thead>
          <tr style={{ background: '#333', color: '#fff' }}>
            <th>Hostname</th>
            <th>Adres MAC</th>
            <th>IP</th>
            <th>System</th>
            <th>Ostatnio widziany</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {devices.map(device => (
            <tr key={device.id}>
              <td><strong>{device.hostname}</strong></td>
              <td style={{ fontFamily: 'monospace' }}>{device.macAddress}</td>
              <td>{device.ipAddress || '-'}</td>
              <td>{device.osVersion}</td>
              <td>{new Date(device.lastSeenAt).toLocaleString()}</td>
              <td>
                {device.status === 0 ? '🟡 Oczekujący' : 
                 device.status === 1 ? '🟢 Aktywny' : '🔴 Zablokowany'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};