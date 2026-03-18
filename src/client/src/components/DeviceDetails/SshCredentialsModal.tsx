import React, { useState } from "react";
import { setSshCredentials } from "../../api/devices";

interface SshCredentialsModalProps {
  deviceId: string;
  initialUseKeyAuth?: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function SshCredentialsModal({
  deviceId,
  initialUseKeyAuth = true,
  onClose,
  onSuccess,
}: SshCredentialsModalProps) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [privateKey, setPrivateKey] = useState("");
  const [useKeyAuth, setUseKeyAuth] = useState(initialUseKeyAuth);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await setSshCredentials(deviceId, {
        username,
        password: password || undefined,
        privateKey: privateKey || undefined,
        useKeyAuthentication: useKeyAuth,
      });
      onSuccess();
    } catch (err: any) {
      setError(err.message || "Failed to set credentials");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/60 backdrop-blur-sm p-8">
      <div className="bg-slate-900 border border-slate-700 rounded-xl shadow-2xl w-full max-w-md p-6">
        <h2 className="text-xl font-semibold text-white mb-4">
          Set SSH Credentials
        </h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-1">
              Username
            </label>
            <input
              type="text"
              required
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-4 py-2 text-white focus:outline-none focus:border-primary-500"
              placeholder="e.g., admin"
            />
          </div>

          <div className="flex items-center mt-4">
            <input
              id="useKeyAuth"
              type="checkbox"
              checked={useKeyAuth}
              onChange={(e) => setUseKeyAuth(e.target.checked)}
              className="w-4 h-4 text-primary-600 bg-slate-800 border-slate-700 rounded focus:ring-primary-500 focus:ring-2"
            />
            <label
              htmlFor="useKeyAuth"
              className="ml-2 text-sm font-medium text-slate-300"
            >
              Use SSH Key Authentication (Recommended)
            </label>
          </div>

          {useKeyAuth ? (
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Private Key (PEM Format)
              </label>
              <textarea
                value={privateKey}
                onChange={(e) => setPrivateKey(e.target.value)}
                rows={5}
                className="w-full bg-slate-800 border border-slate-700 rounded-lg px-4 py-2 text-white focus:outline-none focus:border-primary-500 font-mono text-xs"
                placeholder="-----BEGIN RSA PRIVATE KEY-----... (Content will not be shown after saving)"
              />
              <p className="mt-1 text-xs text-slate-400">
                Key will be stored securely and encrypted. It cannot be viewed
                again once saved.
              </p>
            </div>
          ) : (
            <div>
              <label className="block text-sm font-medium text-slate-300 mb-1">
                Password
              </label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full bg-slate-800 border border-slate-700 rounded-lg px-4 py-2 text-white focus:outline-none focus:border-primary-500"
                placeholder="Leave empty if not changing"
              />
            </div>
          )}

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
              {loading ? "Saving..." : "Save Credentials"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
