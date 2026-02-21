using System;
using System.Runtime.InteropServices;
using LabSync.Modules.SystemInfo.Interfaces;
using LabSync.Modules.SystemInfo.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class SystemInfoService : ISystemInfoService
    {
        private readonly ILogger? _logger;

        public SystemInfoService(ILogger? logger)
        {
            _logger = logger;
        }

        public SystemDetails GetSystemDetails()
        {
            try
            {
                return new SystemDetails
                {
                    OSPlatform = GetPlatformName(),
                    OSDescription = RuntimeInformation.OSDescription,
                    OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    Uptime = GetUptime()
                };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get system info");
                return new SystemDetails();
            }
        }

        private static string GetPlatformName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
            return "Unknown";
        }

        private static string GetUptime()
        {
            try
            {
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
            }
            catch
            {
                return "N/A";
            }
        }
    }
}

