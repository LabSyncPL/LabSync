import React, { useState, useEffect, useMemo } from "react";
import { X } from "lucide-react";
import {
  ScheduledScriptTargetType,
  type ScheduledScriptDto,
} from "../types/scheduledScripts";

interface CreateScheduleModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (data: {
    name: string;
    cronExpression?: string;
    runAt?: string;
    targetType: ScheduledScriptTargetType;
    targetId: string;
  }) => void;
  scriptTitle: string;
  selectedDeviceIds: string[];
  groups: { id: string; name: string }[];
  editData?: ScheduledScriptDto | null;
}

export function CreateScheduleModal({
  isOpen,
  onClose,
  onSave,
  scriptTitle,
  selectedDeviceIds,
  groups,
  editData,
}: CreateScheduleModalProps) {
  const getLocalDatetimeString = (date: Date) => {
    const tzOffset = date.getTimezoneOffset() * 60000;
    const localISOTime = new Date(date.getTime() - tzOffset)
      .toISOString()
      .slice(0, 16);
    return localISOTime;
  };

  const [name, setName] = useState("");
  const [scheduleType, setScheduleType] = useState<"once" | "recurring">(
    "once",
  );
  const [runAt, setRunAt] = useState(
    getLocalDatetimeString(new Date(Date.now() + 3600000)),
  );
  const [cronExpression, setCronExpression] = useState("0 3 * * *");
  const [targetType, setTargetType] = useState<ScheduledScriptTargetType>(
    ScheduledScriptTargetType.SingleAgent,
  );
  const [targetId, setTargetId] = useState("");

  const filteredGroups = useMemo(
    () =>
      groups.filter(
        (g: { id: string; name: string }) => g.id !== "__ungrouped__",
      ),
    [groups],
  );

  const hasGroups = filteredGroups.length > 0;

  useEffect(() => {
    if (!isOpen) return;

    if (editData) {
      setName(editData.name);
      setScheduleType(editData.cronExpression ? "recurring" : "once");
      if (editData.runAt) {
        setRunAt(getLocalDatetimeString(new Date(editData.runAt)));
      }
      if (editData.cronExpression) {
        setCronExpression(editData.cronExpression);
      }
      setTargetType(editData.targetType);
      setTargetId(editData.targetId);
      return;
    }

    setName(`Schedule for ${scriptTitle}`);
    setScheduleType("once");
    const initialTargetType =
      selectedDeviceIds.length === 1 || !hasGroups
        ? ScheduledScriptTargetType.SingleAgent
        : ScheduledScriptTargetType.Group;
    setTargetType(initialTargetType);
    setTargetId(
      initialTargetType === ScheduledScriptTargetType.SingleAgent
        ? selectedDeviceIds[0] || ""
        : filteredGroups[0]?.id || "",
    );
  }, [editData, isOpen, scriptTitle, selectedDeviceIds, filteredGroups, hasGroups]);

  useEffect(() => {
    if (editData) return;

    if (targetType === ScheduledScriptTargetType.SingleAgent) {
      setTargetId(selectedDeviceIds[0] || "");
    } else if (targetType === ScheduledScriptTargetType.Group) {
      setTargetId(filteredGroups[0]?.id || "");
    }
  }, [targetType, selectedDeviceIds, filteredGroups, editData]);

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim()) {
      alert("Please enter a name for the schedule.");
      return;
    }

    if (scheduleType === "recurring" && !cronExpression.trim()) {
      alert("Please enter a valid cron expression.");
      return;
    }

    if (scheduleType === "once" && !runAt) {
      alert("Please select a date and time for the execution.");
      return;
    }

    if (!targetId) {
      alert("Please select a target (agent or group)");
      return;
    }

    try {
      onSave({
        name,
        targetType,
        targetId,
        runAt:
          scheduleType === "once" ? new Date(runAt).toISOString() : undefined,
        cronExpression:
          scheduleType === "recurring" ? cronExpression : undefined,
      });
    } catch (err) {
      console.error("Failed to save schedule:", err);
      alert(
        "An error occurred while saving the schedule. Please check the console for details.",
      );
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
      <div className="w-full max-w-lg rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-2xl overflow-y-auto max-h-[90vh]">
        <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/50 px-6 py-4">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
            {editData ? "Edit Schedule" : "Schedule Script"}
          </h3>
          <button
            onClick={onClose}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 hover:text-slate-900 dark:hover:text-slate-200 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-4 py-2 text-slate-900 dark:text-slate-200 focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none transition-colors"
              placeholder="Enter schedule name..."
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700 dark:text-slate-300">Type</label>
            <select
              value={scheduleType}
              onChange={(e) => setScheduleType(e.target.value as any)}
              className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-3 py-2 text-slate-900 dark:text-slate-200 focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none transition-colors"
            >
              <option value="once">One-time</option>
              <option value="recurring">Recurring (Cron)</option>
            </select>
          </div>

          {scheduleType === "once" ? (
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
                Run At
              </label>
              <input
                type="datetime-local"
                value={runAt}
                onChange={(e) => setRunAt(e.target.value)}
                className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-4 py-2 text-slate-900 dark:text-slate-200 focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none transition-colors"
              />
            </div>
          ) : (
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
                Cron Expression
              </label>
              <input
                type="text"
                value={cronExpression}
                onChange={(e) => setCronExpression(e.target.value)}
                className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-4 py-2 text-slate-900 dark:text-slate-200 font-mono focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none transition-colors"
                placeholder="e.g. 0 3 * * *"
              />
              <p className="text-xs text-slate-500 dark:text-slate-500">
                Minute Hour Day Month DayOfWeek
              </p>
            </div>
          )}

          <div className="space-y-2 pt-2">
            <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
              Target Scope
            </label>
            <div className="flex gap-4 p-3 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="radio"
                  name="schedule-target-type"
                  className="text-primary-600 focus:ring-primary-500 bg-slate-100 dark:bg-slate-700 border-slate-300 dark:border-slate-600"
                  checked={targetType === ScheduledScriptTargetType.SingleAgent}
                  onChange={() =>
                    setTargetType(ScheduledScriptTargetType.SingleAgent)
                  }
                />
                <span className="text-xs font-medium text-slate-700 dark:text-slate-300">Single Agent</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer opacity-80">
                <input
                  type="radio"
                  name="schedule-target-type"
                  className="text-primary-600 focus:ring-primary-500 bg-slate-100 dark:bg-slate-700 border-slate-300 dark:border-slate-600"
                  disabled={!hasGroups}
                  checked={targetType === ScheduledScriptTargetType.Group}
                  onChange={() => setTargetType(ScheduledScriptTargetType.Group)}
                />
                <span className="text-xs font-medium text-slate-700 dark:text-slate-300">Group</span>
              </label>
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
              {targetType === ScheduledScriptTargetType.SingleAgent
                ? "Select Agent"
                : "Select Group"}
            </label>
            <select
              value={targetId}
              onChange={(e) => setTargetId(e.target.value)}
              className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-3 py-2 text-slate-900 dark:text-slate-200 focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none transition-colors"
            >
              {targetType === ScheduledScriptTargetType.SingleAgent ? (
                <>
                  <option value="">Select an agent...</option>
                  {selectedDeviceIds.map((id) => (
                    <option key={id} value={id}>
                      {id}
                    </option>
                  ))}
                </>
              ) : (
                <>
                  <option value="">Select a group...</option>
                  {filteredGroups.map((g) => (
                    <option key={g.id} value={g.id}>
                      {g.name}
                    </option>
                  ))}
                </>
              )}
            </select>
          </div>

          <div className="flex gap-3 pt-4">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 px-4 py-2.5 text-sm font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="flex-1 rounded-lg bg-primary-600 px-4 py-2.5 text-sm font-bold text-white shadow-lg shadow-primary-500/20 hover:bg-primary-500 transition-all active:scale-95"
            >
              {editData ? "Update Schedule" : "Create Schedule"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
