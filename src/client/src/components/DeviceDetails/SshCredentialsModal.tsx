import React, { useState } from 'react';
import { setSshCredentials } from '../../api/devices';

interface SshCredentialsModalProps {
  deviceId: string;
  onClose: () => void;
  onSuccess: () => void;
}

export function SshCredentialsModal({ deviceId, onClose, onSuccess }: SshCredentialsModalProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await setSshCredentials(deviceId, { username, password });
      onSuccess();
    } catch (err: any) {
      setError(err.message || 'Failed to set credentials');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/60 backdrop-blur-sm p-8">
      <div className="bg-slate-900 border border-slate-700 rounded-xl shadow-2xl w-full max-w-md p-6">
        <h2 className="text-xl font-semibold text-white mb-4">Set SSH Credentials</h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-1">Username</label>
            <input
              type="text"
              required
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-4 py-2 text-white focus:outline-none focus:border-primary-500"
              placeholder="e.g., admin"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-1">Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-4 py-2 text-white focus:outline-none focus:border-primary-500"
              placeholder="Leave empty if not changing"
            />
          </div>
          
          {error && <div className="text-red-400 text-sm">{error}</div>}

          <div className="flex justify-end gap-3 mt-6">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-slate-300 hover:text-white"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !username}
              className="px-4 py-2 bg-primary-600 hover:bg-primary-500 text-white rounded-lg disabled:opacity-50"
            >
              {loading ? 'Saving...' : 'Save Credentials'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}