import { useState, useCallback } from 'react';
import { completeSetup } from '../api/system';
import styles from './SetupWizard.module.css';

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
        onSetupComplete();
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
      <div className={styles.container}>
        <p className={styles.success}>
          Setup complete. You can now sign in with the account you created.
        </p>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <h2 className={styles.welcome}>Welcome to LabSync</h2>
      <p className={styles.subtitle}>Create the main administrator account.</p>
      <form className={styles.form} onSubmit={handleSubmit}>
        <label className={styles.label} htmlFor="setup-username">
          Username
        </label>
        <input
          id="setup-username"
          type="text"
          className={styles.input}
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          autoComplete="username"
          minLength={2}
          maxLength={100}
          required
        />
        <label className={styles.label} htmlFor="setup-password">
          Password
        </label>
        <input
          id="setup-password"
          type="password"
          className={styles.input}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoComplete="new-password"
          minLength={6}
          maxLength={200}
          required
        />
        {error && <div className={styles.error}>{error}</div>}
        <button type="submit" className={styles.submitBtn} disabled={isSubmitting}>
          {isSubmitting ? 'Creating account…' : 'Create administrator account'}
        </button>
      </form>
    </div>
  );
}
