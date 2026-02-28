import { useState, useCallback } from "react";
import { createJob } from "../api/jobs";
import type { CreateJobRequest } from "../types/job";

interface CreateJobModalProps {
  deviceId: string;
  onClose: () => void;
  onSuccess: () => void;
}

export function CreateJobModal({
  deviceId,
  onClose,
  onSuccess,
}: CreateJobModalProps) {
  const [command, setCommand] = useState("");
  const [arguments_, setArguments] = useState("");
  const [scriptPayload, setScriptPayload] = useState("");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setError("");
      setIsSubmitting(true);
      try {
        const request: CreateJobRequest = {
          command,
          arguments: arguments_,
          scriptPayload: scriptPayload || undefined,
        };
        await createJob(deviceId, request);
        onSuccess();
        onClose();
      } catch (err: unknown) {
        const message =
          err instanceof Error ? err.message : "Failed to create job.";
        setError(message);
      } finally {
        setIsSubmitting(false);
      }
    },
    [deviceId, command, arguments_, scriptPayload, onClose, onSuccess],
  );

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      onClick={onClose}
    >
      <div
        className="bg-slate-800 rounded-xl border border-slate-700 p-6 max-w-lg w-full shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-xl font-semibold text-white">Create Job</h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-white transition-colors"
            aria-label="Close create job dialog"
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
              ></path>
            </svg>
          </button>
        </div>
        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label
              className="block text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="command"
            >
              Command *
            </label>
            <input
              id="command"
              type="text"
              className="w-full px-4 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={command}
              onChange={(e) => setCommand(e.target.value)}
              placeholder="e.g. winget, apt-get, powershell"
              required
            />
          </div>
          <div>
            <label
              className="block text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="arguments"
            >
              Arguments
            </label>
            <input
              id="arguments"
              type="text"
              className="w-full px-4 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              value={arguments_}
              onChange={(e) => setArguments(e.target.value)}
              placeholder="e.g. install Git -y"
            />
          </div>
          <div>
            <label
              className="block text-slate-300 text-sm font-medium mb-1.5"
              htmlFor="script"
            >
              Script Payload (optional)
            </label>
            <textarea
              id="script"
              rows={4}
              className="w-full px-4 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500 font-mono text-xs"
              value={scriptPayload}
              onChange={(e) => setScriptPayload(e.target.value)}
              placeholder="Full script content (PowerShell/Bash)"
            />
          </div>
          {error && (
            <div className="p-3 bg-danger/10 border border-danger/20 rounded-lg text-danger text-sm">
              {error}
            </div>
          )}
          <div className="flex gap-3">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 bg-slate-700 hover:bg-slate-600 text-white px-4 py-2 rounded-lg font-medium transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="flex-1 bg-primary-600 hover:bg-primary-500 text-white px-4 py-2 rounded-lg font-semibold shadow-lg shadow-primary-500/20 transition-all disabled:opacity-50"
              disabled={isSubmitting}
            >
              {isSubmitting ? "Creating…" : "Create Job"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
