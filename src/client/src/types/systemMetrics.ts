/**
 * System metrics returned by the SystemInfo module (CollectMetrics job).
 * Matches the JSON structure from LabSync.Modules.SystemInfo.
 */
export interface SystemMetricsDto {
  timestamp: string;
  cpuLoad: number;
  memoryInfo: MemoryInfoDto;
  diskInfo: DiskInfoDto;
  systemInfo: SystemDetailsDto;
}

export interface MemoryInfoDto {
  totalMB: number;
  availableMB: number;
  usedMB: number;
  usagePercent: number;
}

export interface DiskInfoDto {
  totalGB: number;
  freeGB: number;
  usedGB: number;
  usagePercent: number;
  driveName: string;
}

export interface SystemDetailsDto {
  oSPlatform: string;
  oSDescription: string;
  oSArchitecture: string;
  processArchitecture: string;
  frameworkDescription: string;
  machineName: string;
  processorCount: number;
  uptime: string;
}

const getNum = (o: Record<string, unknown>, ...keys: string[]): number => {
  for (const k of keys) {
    if (typeof o[k] === 'number') return o[k] as number;
  }
  return 0;
};
const getStr = (o: Record<string, unknown>, ...keys: string[]): string => {
  for (const k of keys) {
    if (typeof o[k] === 'string') return o[k] as string;
  }
  return '';
};

/** Parse SystemInfo module JSON output (PascalCase from C# or camelCase). */
export function parseSystemMetricsFromJson(json: string): SystemMetricsDto | null {
  try {
    const raw = JSON.parse(json) as Record<string, unknown>;
    const m = (raw.MemoryInfo ?? raw.memoryInfo) as Record<string, unknown> | undefined;
    const d = (raw.DiskInfo ?? raw.diskInfo) as Record<string, unknown> | undefined;
    const s = (raw.SystemInfo ?? raw.systemInfo) as Record<string, unknown> | undefined;
    if (!m || !d || !s) return null;

    const ts = raw.Timestamp ?? raw.timestamp;
    const cpu = typeof raw.CpuLoad === 'number' ? raw.CpuLoad : raw.cpuLoad;

    return {
      timestamp: typeof ts === 'string' ? ts : new Date().toISOString(),
      cpuLoad: typeof cpu === 'number' ? cpu : 0,
      memoryInfo: {
        totalMB: getNum(m, 'TotalMB', 'totalMB'),
        availableMB: getNum(m, 'AvailableMB', 'availableMB'),
        usedMB: getNum(m, 'UsedMB', 'usedMB'),
        usagePercent: getNum(m, 'UsagePercent', 'usagePercent'),
      },
      diskInfo: {
        totalGB: getNum(d, 'TotalGB', 'totalGB'),
        freeGB: getNum(d, 'FreeGB', 'freeGB'),
        usedGB: getNum(d, 'UsedGB', 'usedGB'),
        usagePercent: getNum(d, 'UsagePercent', 'usagePercent'),
        driveName: getStr(d, 'DriveName', 'driveName'),
      },
      systemInfo: {
        oSPlatform: getStr(s, 'OSPlatform', 'oSPlatform'),
        oSDescription: getStr(s, 'OSDescription', 'oSDescription'),
        oSArchitecture: getStr(s, 'OSArchitecture', 'oSArchitecture'),
        processArchitecture: getStr(s, 'ProcessArchitecture', 'processArchitecture'),
        frameworkDescription: getStr(s, 'FrameworkDescription', 'frameworkDescription'),
        machineName: getStr(s, 'MachineName', 'machineName'),
        processorCount: getNum(s, 'ProcessorCount', 'processorCount'),
        uptime: getStr(s, 'Uptime', 'uptime'),
      },
    };
  } catch {
    return null;
  }
}
