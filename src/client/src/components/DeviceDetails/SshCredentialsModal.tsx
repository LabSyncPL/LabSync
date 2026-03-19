import React, { useState, useEffect } from "react";
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
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    setIsVisible(true);
  }, []);

  const handleClose = () => {
    setIsVisible(false);
    setTimeout(onClose, 300); // match transition duration
  };

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
      setError(
        err.message || "An error occurred while saving SSH credentials. Please try again."
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div
      className={`fixed inset-0 z-[60] flex items-center justify-center p-4 sm:p-8 transition-opacity duration-300 ${
        isVisible ? "opacity-100" : "opacity-0"
      }`}
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-title"
    >
      <div 
        className="absolute inset-0 bg-slate-900/40 dark:bg-black/60 backdrop-blur-sm transition-opacity" 
        onClick={handleClose}
        aria-hidden="true"
      />
      <div
        className={`relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl shadow-2xl w-full max-w-md p-6 transform transition-all duration-300 ${
          isVisible ? "scale-100 translate-y-0" : "scale-95 translate-y-4"
        }`}
      >
        <div className="flex justify-between items-center mb-6">
          <h2 id="modal-title" className="text-xl font-semibold text-slate-900 dark:text-white">
            SSH Connection Setup
          </h2>
          <button
            onClick={handleClose}
            className="text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-white transition-colors"
            aria-label="Close window"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label htmlFor="username" className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5">
              Username
            </label>
            <input
              id="username"
              type="text"
              required
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full bg-slate-50 dark:bg-slate-800 border border-slate-300 dark:border-slate-700 rounded-lg px-4 py-2.5 text-slate-900 dark:text-white placeholder-slate-400 dark:placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow"
              placeholder="e.g. admin"
              aria-required="true"
              disabled={loading}
            />
          </div>

          <div className="flex items-center bg-slate-50 dark:bg-slate-800/50 p-3 rounded-lg border border-slate-200 dark:border-slate-700/50">
            <input
              id="useKeyAuth"
              type="checkbox"
              checked={useKeyAuth}
              onChange={(e) => setUseKeyAuth(e.target.checked)}
              className="w-4 h-4 text-primary-600 bg-white dark:bg-slate-900 border-slate-300 dark:border-slate-600 rounded focus:ring-primary-500 focus:ring-2 cursor-pointer transition-colors"
              disabled={loading}
            />
            <label
              htmlFor="useKeyAuth"
              className="ml-3 text-sm font-medium text-slate-700 dark:text-slate-300 cursor-pointer select-none"
            >
              Use SSH Key (Recommended)
            </label>
          </div>

          <div className={`transition-all duration-300 overflow-hidden ${useKeyAuth ? 'max-h-64 opacity-100' : 'max-h-24 opacity-100'}`}>
            {useKeyAuth ? (
              <div>
                <label htmlFor="privateKey" className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5">
                  Private Key (PEM Format)
                </label>
                <textarea
                  id="privateKey"
                  value={privateKey}
                  onChange={(e) => setPrivateKey(e.target.value)}
                  rows={4}
                  className="w-full bg-slate-50 dark:bg-slate-800 border border-slate-300 dark:border-slate-700 rounded-lg px-4 py-2.5 text-slate-900 dark:text-white placeholder-slate-400 dark:placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500 font-mono text-xs resize-none transition-shadow"
                  placeholder="-----BEGIN RSA PRIVATE KEY-----..."
                  disabled={loading}
                  aria-describedby="privateKey-help"
                />
                <p id="privateKey-help" className="mt-1.5 text-xs text-slate-500 dark:text-slate-400 flex items-center gap-1">
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
                  Key will be securely encrypted.
                </p>
              </div>
            ) : (
              <div>
                <label htmlFor="password" className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5">
                  Password
                </label>
                <input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full bg-slate-50 dark:bg-slate-800 border border-slate-300 dark:border-slate-700 rounded-lg px-4 py-2.5 text-slate-900 dark:text-white placeholder-slate-400 dark:placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow"
                  placeholder="Leave empty if not changing"
                  disabled={loading}
                />
              </div>
            )}
          </div>

          {error && (
            <div className="flex items-start gap-2 p-3 bg-danger/10 dark:bg-danger/20 border border-danger/20 dark:border-danger/30 rounded-lg" role="alert">
              <svg className="w-5 h-5 text-danger shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <span className="text-sm text-danger-700 dark:text-danger-400 font-medium">{error}</span>
            </div>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={handleClose}
              disabled={loading}
              className="px-4 py-2 text-sm font-medium text-slate-700 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white bg-transparent hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors focus:outline-none focus:ring-2 focus:ring-slate-400"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !username}
              className="relative px-5 py-2 text-sm font-medium bg-primary-600 hover:bg-primary-500 text-white rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 dark:focus:ring-offset-slate-900 flex items-center justify-center min-w-[140px]"
            >
              {loading ? (
                <>
                  <svg className="animate-spin -ml-1 mr-2 h-4 w-4 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  Saving...
                </>
              ) : (
                "Save Credentials"
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
