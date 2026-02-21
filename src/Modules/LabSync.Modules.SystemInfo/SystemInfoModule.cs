using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LabSync.Core.Interfaces;
using LabSync.Modules.SystemInfo.Interfaces;
using LabSync.Modules.SystemInfo.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class SystemInfoModule : IAgentModule
    {
        public string Name => "SystemInfo";
        public string Version => "3.0.0";

        private ILogger? _logger;
        private ICpuInfoService? _cpuInfoService;
        private IMemoryInfoService? _memoryInfoService;
        private IDiskInfoService? _diskInfoService;
        private INetworkInfoService? _networkInfoService;
        private ISystemInfoService? _systemInfoService;

        public Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var loggerFactory = (ILoggerFactory?)serviceProvider.GetService(typeof(ILoggerFactory));
            _logger = loggerFactory?.CreateLogger<SystemInfoModule>();

            _cpuInfoService = new CpuInfoService(loggerFactory?.CreateLogger<CpuInfoService>());
            _memoryInfoService = new MemoryInfoService(loggerFactory?.CreateLogger<MemoryInfoService>());
            _diskInfoService = new DiskInfoService(loggerFactory?.CreateLogger<DiskInfoService>());
            _networkInfoService = new NetworkInfoService(loggerFactory?.CreateLogger<NetworkInfoService>());
            _systemInfoService = new SystemInfoService(loggerFactory?.CreateLogger<SystemInfoService>());

            _logger?.LogInformation("SystemInfo module initialized. Platform: {Platform}",
                RuntimeInformation.OSDescription);

            return Task.CompletedTask;
        }

        public bool CanHandle(string jobType)
        {
            return jobType.Equals("CollectMetrics", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("Get-SysInfo", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("SystemInfo", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Collecting system metrics...");

                var metrics = await CollectSystemMetricsAsync(cancellationToken);

                _logger?.LogDebug("Successfully collected system metrics. CPU: {Cpu}%, Memory: {Memory}%, Disk: {Disk}%, Network RX: {NetworkRx}",
                    metrics.CpuLoad, metrics.MemoryInfo.UsagePercent, metrics.DiskInfo.UsagePercent, metrics.NetworkInfo.TotalBytesReceivedPerSecond);

                return ModuleResult.Success(metrics);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("System metrics collection was cancelled.");
                return ModuleResult.Failure("Operation was cancelled.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error collecting system metrics");
                return ModuleResult.Failure($"Failed to collect metrics: {ex.Message}");
            }
        }

        private async Task<SystemMetrics> CollectSystemMetricsAsync(CancellationToken cancellationToken)
        {
            if (_cpuInfoService == null ||
                _memoryInfoService == null ||
                _diskInfoService == null ||
                _networkInfoService == null ||
                _systemInfoService == null)
            {
                throw new InvalidOperationException("SystemInfoModule has not been initialized.");
            }

            var cpuTask = Task.Run(_cpuInfoService.GetCpuUsage, cancellationToken);
            var memoryTask = Task.Run(_memoryInfoService.GetMemoryInfo, cancellationToken);
            var diskTask = Task.Run(_diskInfoService.GetDiskInfo, cancellationToken);
            var networkTask = Task.Run(_networkInfoService.GetNetworkInfo, cancellationToken);
            var systemInfoTask = Task.Run(_systemInfoService.GetSystemDetails, cancellationToken);

            await Task.WhenAll(cpuTask, memoryTask, diskTask, networkTask, systemInfoTask);

            return new SystemMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuLoad = cpuTask.Result,
                MemoryInfo = memoryTask.Result,
                DiskInfo = diskTask.Result,
                NetworkInfo = networkTask.Result,
                SystemInfo = systemInfoTask.Result
            };
        }
    }
}
