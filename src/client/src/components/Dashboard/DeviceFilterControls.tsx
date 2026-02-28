import { DevicePlatform } from "../../types/device";

export interface DeviceFilters {
  search: string;
  status: "all" | "online" | "offline" | "pending";
  platform: "all" | DevicePlatform;
  group: "all" | "no-group" | string;
  viewMode: "grid" | "list";
}

interface DeviceFilterControlsProps {
  filters: DeviceFilters;
  groups: string[];
  onFilterChange: (filters: DeviceFilters) => void;
  onRefresh: () => void;
  isRefreshing: boolean;
}

export function DeviceFilterControls({
  filters,
  groups,
  onFilterChange,
  onRefresh,
  isRefreshing,
}: DeviceFilterControlsProps) {
  const handleChange = (key: keyof DeviceFilters, value: any) => {
    onFilterChange({ ...filters, [key]: value });
  };

  return (
    <div className="flex flex-col md:flex-row gap-4 items-center justify-between bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm mb-6">
      <div className="flex flex-1 gap-3 w-full md:w-auto flex-wrap">
        <div className="relative flex-1 min-w-[200px] md:max-w-xs">
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <svg
              className="h-4 w-4 text-slate-500"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
              />
            </svg>
          </div>
          <input
            type="text"
            placeholder="Search devices..."
            value={filters.search}
            onChange={(e) => handleChange("search", e.target.value)}
            className="pl-10 pr-4 py-2 w-full bg-slate-900 border border-slate-700 rounded-lg text-sm text-white placeholder-slate-500 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500 transition-colors"
          />
        </div>

        <select
          value={filters.status}
          onChange={(e) => handleChange("status", e.target.value)}
          className="bg-slate-900 border border-slate-700 text-white text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block p-2 px-3 min-w-[120px]"
        >
          <option value="all">All Status</option>
          <option value="online">Online</option>
          <option value="offline">Offline</option>
          <option value="pending">Pending</option>
        </select>

        <select
          value={filters.group}
          onChange={(e) => handleChange("group", e.target.value)}
          className="bg-slate-900 border border-slate-700 text-white text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block p-2 px-3 min-w-[120px]"
        >
          <option value="all">All Groups</option>
          <option value="no-group">No Group</option>
          {groups.map((group) => (
            <option key={group} value={group}>
              {group}
            </option>
          ))}
        </select>

        <select
          value={filters.platform}
          onChange={(e) =>
            handleChange("platform", Number(e.target.value) || "all")
          }
          className="bg-slate-900 border border-slate-700 text-white text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block p-2 px-3 hidden sm:block"
        >
          <option value="all">All Platforms</option>
          <option value={DevicePlatform.Windows}>Windows</option>
          <option value={DevicePlatform.Linux}>Linux</option>
          <option value={DevicePlatform.MacOS}>macOS</option>
        </select>
      </div>

      <div className="flex items-center gap-3 w-full md:w-auto justify-end">
        <div className="flex bg-slate-900 rounded-lg p-1 border border-slate-700">
          <button
            onClick={() => handleChange("viewMode", "grid")}
            className={`p-1.5 rounded transition-colors ${
              filters.viewMode === "grid"
                ? "bg-slate-700 text-white shadow-sm"
                : "text-slate-500 hover:text-slate-300"
            }`}
            title="Grid View"
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z"
              />
            </svg>
          </button>
          <button
            onClick={() => handleChange("viewMode", "list")}
            className={`p-1.5 rounded transition-colors ${
              filters.viewMode === "list"
                ? "bg-slate-700 text-white shadow-sm"
                : "text-slate-500 hover:text-slate-300"
            }`}
            title="List View"
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M4 6h16M4 12h16M4 18h16"
              />
            </svg>
          </button>
        </div>

        <div className="h-6 w-px bg-slate-700 mx-1"></div>

        <button
          onClick={onRefresh}
          disabled={isRefreshing}
          className="flex items-center gap-2 px-3 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <svg
            className={`w-4 h-4 ${isRefreshing ? "animate-spin" : ""}`}
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
            />
          </svg>
          <span className="hidden sm:inline">
            {isRefreshing ? "Refreshing..." : "Refresh"}
          </span>
        </button>
      </div>
    </div>
  );
}
