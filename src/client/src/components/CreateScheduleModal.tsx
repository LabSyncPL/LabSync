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

  useEffect(() => {
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
    } else {
      setName(`Schedule for ${scriptTitle}`);
      setScheduleType("once");
      const initialTargetType =
        selectedDeviceIds.length === 1
          ? ScheduledScriptTargetType.SingleAgent
          : ScheduledScriptTargetType.Group;
      setTargetType(initialTargetType);

      if (initialTargetType === ScheduledScriptTargetType.SingleAgent) {
        setTargetId(selectedDeviceIds[0] || "");
      } else {
        setTargetId(filteredGroups[0]?.id || "");
      }
    }
  }, [editData, scriptTitle, isOpen, selectedDeviceIds, filteredGroups]);

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 backdrop-blur-sm p-4">
      <div className="w-full max-w-lg rounded-xl border border-slate-800 bg-slate-900 shadow-2xl overflow-y-auto max-h-[90vh]">
        <div className="flex items-center justify-between border-b border-slate-800 bg-slate-900/50 px-6 py-4">
          <h3 className="text-lg font-semibold text-slate-100">
            {editData ? "Edit Schedule" : "Schedule Script"}
          </h3>
          <button
            onClick={onClose}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-800 hover:text-slate-200"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-300">Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-lg border border-slate-700 bg-slate-800 px-4 py-2 text-slate-200 focus:border-blue-500 focus:outline-none"
              placeholder="Enter schedule name..."
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-300">Type</label>
            <select
              value={scheduleType}
              onChange={(e) => setScheduleType(e.target.value as any)}
              className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-slate-200 focus:border-blue-500 focus:outline-none"
            >
              <option value="once">One-time</option>
              <option value="recurring">Recurring (Cron)</option>
            </select>
          </div>

          {scheduleType === "once" ? (
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-300">
                Run At
              </label>
              <input
                type="datetime-local"
                value={runAt}
                onChange={(e) => setRunAt(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-800 px-4 py-2 text-slate-200 focus:border-blue-500 focus:outline-none"
              />
            </div>
          ) : (
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-300">
                Cron Expression
              </label>
              <input
                type="text"
                value={cronExpression}
                onChange={(e) => setCronExpression(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-800 px-4 py-2 text-slate-200 focus:border-blue-500 focus:outline-none"
                placeholder="e.g. 0 3 * * *"
              />
              <p className="text-xs text-slate-500">
                Minute Hour Day Month DayOfWeek
              </p>
            </div>
          )}

          <div className="space-y-2 pt-2">
            <label className="text-sm font-medium text-slate-300">
              Target Scope
            </label>
            <div className="flex gap-4 p-3 rounded-lg bg-slate-800/50 border border-slate-700">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="radio"
                  checked={targetType === ScheduledScriptTargetType.SingleAgent}
                  onChange={() =>
                    setTargetType(ScheduledScriptTargetType.SingleAgent)
                  }
                  className="text-blue-500 focus:ring-blue-500"
                />
                <span className="text-sm text-slate-300">Single Agent</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="radio"
                  checked={targetType === ScheduledScriptTargetType.Group}
                  onChange={() =>
                    setTargetType(ScheduledScriptTargetType.Group)
                  }
                  className="text-blue-500 focus:ring-blue-500"
                />
                <span className="text-sm text-slate-300">Group</span>
              </label>
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-300">
              {targetType === ScheduledScriptTargetType.SingleAgent
                ? "Select Agent"
                : "Select Group"}
            </label>
            <select
              value={targetId}
              onChange={(e) => setTargetId(e.target.value)}
              className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-slate-200 focus:border-blue-500 focus:outline-none"
            >
              <option value="" disabled>
                Select target...
              </option>
              {targetType === ScheduledScriptTargetType.SingleAgent
                ? selectedDeviceIds.map((id) => (
                    <option key={id} value={id}>
                      Agent {id.slice(0, 8)}
                    </option>
                  ))
                : filteredGroups.map((g: { id: string; name: string }) => (
                    <option key={g.id} value={g.id}>
                      {g.name}
                    </option>
                  ))}
            </select>
          </div>

          <div className="flex justify-end gap-3 pt-4 border-t border-slate-800">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg px-4 py-2 text-sm font-medium text-slate-400 hover:bg-slate-800 hover:text-slate-200"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-500"
            >
              {editData ? "Update" : "Schedule"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
