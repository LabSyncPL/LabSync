import { useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { login } from "../api/auth";
import { setToken } from "../auth/authStore";

interface LoginProps {
  onSetupRequired?: () => void;
}

export function Login({ onSetupRequired }: LoginProps) {
  const navigate = useNavigate();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setError("");
      setIsSubmitting(true);
      try {
        const res = await login({ username, password });
        setToken(res.accessToken);
        navigate("/");
      } catch (err: unknown) {
        const status =
          err && typeof err === "object" && "response" in err
            ? (err as { response?: { status?: number } }).response?.status
            : undefined;
        if (status === 503 && onSetupRequired) {
          onSetupRequired();
          return;
        }
        const message = err instanceof Error ? err.message : "Login failed.";
        setError(message);
      } finally {
        setIsSubmitting(false);
      }
    },
    [username, password, onSetupRequired, navigate],
  );

  return (
    <div className="max-w-md w-full mx-auto">
      <div className="bg-slate-800 rounded-xl border border-slate-700 p-8 shadow-xl">
        <h2 className="text-2xl font-semibold text-white mb-2 text-center">
          Admin login
        </h2>
        <p className="text-slate-400 text-sm text-center mb-6">
          Sign in to access LabSync
        </p>
        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label
              className="block text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="username"
            >
              Username
            </label>
            <input
              id="username"
              type="text"
              className="w-full px-4 py-2.5 bg-slate-900 border border-slate-700 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              required
            />
          </div>
          <div>
            <label
              className="block text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="password"
            >
              Password
            </label>
            <input
              id="password"
              type="password"
              className="w-full px-4 py-2.5 bg-slate-900 border border-slate-700 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
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
            {isSubmitting ? "Signing in…" : "Sign in"}
          </button>
        </form>
      </div>
    </div>
  );
}
