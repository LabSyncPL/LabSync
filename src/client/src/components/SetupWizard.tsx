import { useState, useCallback } from 'react';
import { completeSetup } from '../api/system';

interface SetupWizardProps {
  onSetupComplete: () => void;
}

export function SetupWizard({ onSetupComplete }: SetupWizardProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [success, setSuccess] = useState(false);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setError('');
      setIsSubmitting(true);
      try {
        await completeSetup({ username, password });
        setSuccess(true);
        setTimeout(() => {
          onSetupComplete();
        }, 1500);
      } catch (err: unknown) {
        const message =
          err && typeof err === 'object' && 'response' in err
            ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
            : 'Setup failed.';
        setError(message ?? 'Setup failed.');
      } finally {
        setIsSubmitting(false);
      }
    },
    [username, password, onSetupComplete]
  );

  if (success) {
    return (
      <div className="max-w-md w-full mx-auto">
        <div className="bg-slate-800 rounded-xl border border-slate-700 p-8 shadow-xl">
          <div className="text-center">
            <div className="w-16 h-16 bg-success/20 rounded-full flex items-center justify-center mx-auto mb-4">
              <svg className="w-8 h-8 text-success" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 13l4 4L19 7"></path>
              </svg>
            </div>
            <p className="text-success font-medium text-lg mb-2">Setup complete!</p>
            <p className="text-slate-400 text-sm">You can now sign in with the account you created.</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-md w-full mx-auto">
      <div className="bg-slate-800 rounded-xl border border-slate-700 p-8 shadow-xl">
        <h2 className="text-2xl font-bold text-white mb-2 text-center">Welcome to LabSync</h2>
        <p className="text-slate-400 text-sm text-center mb-6">Create the main administrator account.</p>
        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label className="block text-slate-300 text-sm font-medium mb-1.5" htmlFor="setup-username">
              Username
            </label>
            <input
              id="setup-username"
              type="text"
              className="w-full px-4 py-2.5 bg-slate-900 border border-slate-700 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              minLength={2}
              maxLength={100}
              required
            />
          </div>
          <div>
            <label className="block text-slate-300 text-sm font-medium mb-1.5" htmlFor="setup-password">
              Password
            </label>
            <input
              id="setup-password"
              type="password"
              className="w-full px-4 py-2.5 bg-slate-900 border border-slate-700 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
              minLength={6}
              maxLength={200}
              required
            />
            <p className="text-slate-500 text-xs mt-1">Minimum 6 characters</p>
          </div>
          {error && (
            <div className="p-3 bg-danger/10 border border-danger/20 rounded-lg text-danger text-sm">
              {error}
            </div>
          )}
          <button
            type="submit"
            className="w-full bg-success hover:bg-success/90 text-white px-4 py-2.5 rounded-lg font-semibold shadow-lg shadow-success/20 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Creating account…' : 'Create administrator account'}
          </button>
        </form>
      </div>
    </div>
  );
}
