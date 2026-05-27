import { useState, useCallback } from "react";
import { registerAccount } from "../api/auth";

interface RegisterAccountProps {
  onBackToLogin: () => void;
}

export function RegisterAccount({ onBackToLogin }: RegisterAccountProps) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setError("");

      if (password !== confirmPassword) {
        setError("Passwords do not match.");
        return;
      }

      setIsSubmitting(true);
      try {
        await registerAccount({ username, password });
        setSuccess(true);
      } catch (err: unknown) {
        const message =
          err && typeof err === "object" && "message" in err
            ? String((err as { message?: string }).message)
            : "Registration failed.";
        setError(message || "Registration failed.");
      } finally {
        setIsSubmitting(false);
      }
    },
    [username, password, confirmPassword],
  );

  if (success) {
    return (
      <div className="max-w-md w-full mx-auto">
        <div className="bg-white dark:bg-slate-800 rounded-xl border border-slate-300 dark:border-slate-700 p-8 shadow-xl">
          <div className="text-center space-y-4">
            <div className="w-16 h-16 bg-success/20 rounded-full flex items-center justify-center mx-auto">
              <svg
                className="w-8 h-8 text-success"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M5 13l4 4L19 7"
                />
              </svg>
            </div>
            <p className="text-success font-medium text-lg">Account created</p>
            <p className="text-slate-600 dark:text-slate-400 text-sm">
              You can now sign in with your new credentials.
            </p>
            <button
              type="button"
              onClick={onBackToLogin}
              className="w-full bg-primary-600 hover:bg-primary-500 text-white px-4 py-2.5 rounded-lg font-semibold transition-all"
            >
              Back to sign in
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-md w-full mx-auto">
      <div className="bg-white dark:bg-slate-800 rounded-xl border border-slate-300 dark:border-slate-700 p-8 shadow-xl">
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2 text-center">
          Create account
        </h2>
        <p className="text-slate-600 dark:text-slate-400 text-sm text-center mb-6">
          Register a new administrator account
        </p>
        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label
              className="block text-slate-700 dark:text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="register-username"
            >
              Username
            </label>
            <input
              id="register-username"
              type="text"
              className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-900 dark:text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              minLength={2}
              maxLength={100}
              required
            />
          </div>
          <div>
            <label
              className="block text-slate-700 dark:text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="register-password"
            >
              Password
            </label>
            <input
              id="register-password"
              type="password"
              className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-900 dark:text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
              minLength={6}
              maxLength={200}
              required
            />
            <p className="text-slate-500 dark:text-slate-400 text-xs mt-1">Minimum 6 characters</p>
          </div>
          <div>
            <label
              className="block text-slate-700 dark:text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="register-confirm-password"
            >
              Confirm password
            </label>
            <input
              id="register-confirm-password"
              type="password"
              className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-900 dark:text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              autoComplete="new-password"
              minLength={6}
              maxLength={200}
              required
            />
          </div>
          {error && (
            <div className="p-3 bg-danger/10 border border-danger/20 rounded-lg text-danger text-sm">
              {error}
            </div>
          )}
          <button
            type="submit"
            className="w-full bg-primary-600 hover:bg-primary-500 text-white px-4 py-2.5 rounded-lg font-semibold shadow-lg shadow-primary-500/20 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
            disabled={isSubmitting}
          >
            {isSubmitting ? "Creating account…" : "Create account"}
          </button>
        </form>
        <p className="text-center text-slate-600 dark:text-slate-400 text-sm mt-6">
          Already have an account?{" "}
          <button
            type="button"
            onClick={onBackToLogin}
            className="text-primary-400 hover:text-primary-300 font-medium"
          >
            Sign in
          </button>
        </p>
      </div>
    </div>
  );
}
