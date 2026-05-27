import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  changePassword,
  changeUsername,
  fetchAccountProfile,
} from "../api/auth";
import { extractApiErrorMessage } from "../api/scriptRunner";
import { clearToken, setToken } from "../auth/authStore";
import type { AccountProfile } from "../types/auth";

interface AccountSettingsModalProps {
  onClose: () => void;
}

const inputClassName =
  "w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-900 dark:text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500 transition-colors";

export function AccountSettingsModal({ onClose }: AccountSettingsModalProps) {
  const navigate = useNavigate();
  const [isVisible, setIsVisible] = useState(false);
  const [profile, setProfile] = useState<AccountProfile | null>(null);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileLoading, setProfileLoading] = useState(true);

  const [newUsername, setNewUsername] = useState("");
  const [usernameCurrentPassword, setUsernameCurrentPassword] = useState("");
  const [usernameError, setUsernameError] = useState<string | null>(null);
  const [usernameSuccess, setUsernameSuccess] = useState<string | null>(null);
  const [usernameSubmitting, setUsernameSubmitting] = useState(false);

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);
  const [passwordSubmitting, setPasswordSubmitting] = useState(false);

  useEffect(() => {
    setIsVisible(true);
  }, []);

  const loadProfile = useCallback(async () => {
    setProfileLoading(true);
    setProfileError(null);
    try {
      const data = await fetchAccountProfile();
      setProfile(data);
      setNewUsername(data.username);
    } catch (err: unknown) {
      setProfileError(extractApiErrorMessage(err));
    } finally {
      setProfileLoading(false);
    }
  }, []);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  const handleClose = () => {
    setIsVisible(false);
    setTimeout(onClose, 300);
  };

  const handleLogout = () => {
    clearToken();
    navigate("/login");
    handleClose();
  };

  const handleChangeUsername = async (e: React.FormEvent) => {
    e.preventDefault();
    setUsernameError(null);
    setUsernameSuccess(null);

    const trimmed = newUsername.trim();
    if (!trimmed) {
      setUsernameError("Username is required.");
      return;
    }

    if (trimmed === profile?.username) {
      setUsernameError("Choose a different username.");
      return;
    }

    setUsernameSubmitting(true);
    try {
      const res = await changeUsername({
        newUsername: trimmed,
        currentPassword: usernameCurrentPassword,
      });
      setToken(res.accessToken);
      setUsernameCurrentPassword("");
      setUsernameSuccess("Username updated.");
      await loadProfile();
    } catch (err: unknown) {
      setUsernameError(extractApiErrorMessage(err));
    } finally {
      setUsernameSubmitting(false);
    }
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordError(null);
    setPasswordSuccess(null);

    if (newPassword !== confirmNewPassword) {
      setPasswordError("New passwords do not match.");
      return;
    }

    setPasswordSubmitting(true);
    try {
      const res = await changePassword({
        currentPassword,
        newPassword,
      });
      setToken(res.accessToken);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmNewPassword("");
      setPasswordSuccess("Password updated.");
    } catch (err: unknown) {
      setPasswordError(extractApiErrorMessage(err));
    } finally {
      setPasswordSubmitting(false);
    }
  };

  const createdAtLabel = profile
    ? new Date(profile.createdAt).toLocaleDateString(undefined, {
        year: "numeric",
        month: "long",
        day: "numeric",
      })
    : null;

  return (
    <div
      className={`fixed inset-0 z-[60] flex items-center justify-center p-4 sm:p-8 transition-opacity duration-300 ${
        isVisible ? "opacity-100" : "opacity-0"
      }`}
      role="dialog"
      aria-modal="true"
      aria-labelledby="account-modal-title"
    >
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={handleClose}
        aria-hidden="true"
      />
      <div
        className={`relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto transform transition-all duration-300 ${
          isVisible ? "scale-100 translate-y-0" : "scale-95 translate-y-4"
        }`}
      >
        <div className="sticky top-0 z-10 flex justify-between items-center px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-white dark:bg-slate-900">
          <h2
            id="account-modal-title"
            className="text-xl font-semibold text-slate-900 dark:text-white"
          >
            Account
          </h2>
          <button
            type="button"
            onClick={handleClose}
            className="text-slate-400 hover:text-slate-900 dark:hover:text-white transition-colors p-1 rounded-md hover:bg-slate-100 dark:hover:bg-slate-800"
            aria-label="Close"
          >
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>

        <div className="p-6 space-y-8">
          <section>
            <h3 className="text-sm font-medium text-slate-500 dark:text-slate-300 mb-3 uppercase tracking-wider">
              Profile
            </h3>
            {profileLoading ? (
              <p className="text-slate-500 text-sm">Loading…</p>
            ) : profileError ? (
              <p className="text-danger text-sm">{profileError}</p>
            ) : profile ? (
              <dl className="space-y-2 text-sm">
                <div className="flex justify-between gap-4">
                  <dt className="text-slate-500">Username</dt>
                  <dd className="text-slate-900 dark:text-white font-medium truncate">
                    {profile.username}
                  </dd>
                </div>
                {createdAtLabel && (
                  <div className="flex justify-between gap-4">
                    <dt className="text-slate-500">Member since</dt>
                    <dd className="text-slate-600 dark:text-slate-300">
                      {createdAtLabel}
                    </dd>
                  </div>
                )}
              </dl>
            ) : null}
          </section>

          <section>
            <h3 className="text-sm font-medium text-slate-500 dark:text-slate-300 mb-3 uppercase tracking-wider">
              Change username
            </h3>
            <form className="space-y-3" onSubmit={handleChangeUsername}>
              <div>
                <label
                  htmlFor="account-new-username"
                  className="block text-slate-500 dark:text-slate-400 text-xs font-medium mb-1.5"
                >
                  New username
                </label>
                <input
                  id="account-new-username"
                  type="text"
                  className={inputClassName}
                  value={newUsername}
                  onChange={(e) => setNewUsername(e.target.value)}
                  minLength={2}
                  maxLength={100}
                  required
                />
              </div>
              <div>
                <label
                  htmlFor="account-username-password"
                  className="block text-slate-500 dark:text-slate-400 text-xs font-medium mb-1.5"
                >
                  Current password
                </label>
                <input
                  id="account-username-password"
                  type="password"
                  className={inputClassName}
                  value={usernameCurrentPassword}
                  onChange={(e) => setUsernameCurrentPassword(e.target.value)}
                  autoComplete="current-password"
                  required
                />
              </div>
              {usernameError && (
                <p className="text-danger text-sm">{usernameError}</p>
              )}
              {usernameSuccess && (
                <p className="text-success text-sm">{usernameSuccess}</p>
              )}
              <button
                type="submit"
                disabled={usernameSubmitting}
                className="w-full bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-900 dark:text-white px-4 py-2 rounded-lg text-sm font-medium border border-slate-200 dark:border-slate-700 transition-colors disabled:opacity-50"
              >
                {usernameSubmitting ? "Saving…" : "Update username"}
              </button>
            </form>
          </section>

          <section>
            <h3 className="text-sm font-medium text-slate-500 dark:text-slate-300 mb-3 uppercase tracking-wider">
              Change password
            </h3>
            <form className="space-y-3" onSubmit={handleChangePassword}>
              <div>
                <label
                  htmlFor="account-current-password"
                  className="block text-slate-500 dark:text-slate-400 text-xs font-medium mb-1.5"
                >
                  Current password
                </label>
                <input
                  id="account-current-password"
                  type="password"
                  className={inputClassName}
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  autoComplete="current-password"
                  required
                />
              </div>
              <div>
                <label
                  htmlFor="account-new-password"
                  className="block text-slate-500 dark:text-slate-400 text-xs font-medium mb-1.5"
                >
                  New password
                </label>
                <input
                  id="account-new-password"
                  type="password"
                  className={inputClassName}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  autoComplete="new-password"
                  minLength={6}
                  maxLength={200}
                  required
                />
              </div>
              <div>
                <label
                  htmlFor="account-confirm-password"
                  className="block text-slate-500 dark:text-slate-400 text-xs font-medium mb-1.5"
                >
                  Confirm new password
                </label>
                <input
                  id="account-confirm-password"
                  type="password"
                  className={inputClassName}
                  value={confirmNewPassword}
                  onChange={(e) => setConfirmNewPassword(e.target.value)}
                  autoComplete="new-password"
                  minLength={6}
                  maxLength={200}
                  required
                />
              </div>
              {passwordError && (
                <p className="text-danger text-sm">{passwordError}</p>
              )}
              {passwordSuccess && (
                <p className="text-success text-sm">{passwordSuccess}</p>
              )}
              <button
                type="submit"
                disabled={passwordSubmitting}
                className="w-full bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-900 dark:text-white px-4 py-2 rounded-lg text-sm font-medium border border-slate-200 dark:border-slate-700 transition-colors disabled:opacity-50"
              >
                {passwordSubmitting ? "Saving…" : "Update password"}
              </button>
            </form>
          </section>

          <section className="pt-2 border-t border-slate-100 dark:border-slate-800">
            <button
              type="button"
              onClick={handleLogout}
              className="w-full text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 px-4 py-2.5 rounded-lg text-sm font-medium border border-slate-200 dark:border-slate-700 transition-colors"
            >
              Log out
            </button>
          </section>
        </div>
      </div>
    </div>
  );
}
